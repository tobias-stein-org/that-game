using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;

using UnityEngine;

using Unity.Entities;
using Unity.Collections;

using TBlackboard = Unity.Collections.NativeHashMap<Unity.Collections.FixedString64Bytes, Unity.Collections.FixedString128Bytes>;

namespace tg.ai.behaviour.tree
{
    using tg.application.entities;

    using tg.ai.entities;
    using tg.ai.steering.entities;

    public struct State : IEquatable<State>
    {
        private int value;

        private State(int value) { this.value = value; }

        public bool Equals(State other) { return this.value.Equals(other.value); }
        public override int GetHashCode() { return this.value.GetHashCode(); }

        public static implicit operator State(int value) { return new State(value); }
        public static implicit operator int(State state) { return state.value; }

        #region STATES
     
        public const int success = 1;
        public const int failure = 2;
        public const int running = 3;

        #endregion
    }


    public class Blackboard : IDisposable
    {
        private TBlackboard     board;

        public Blackboard()
        {
            UnityEngine.Debug.Log("Alloc new blackboard");
            this.board = new TBlackboard(8, Allocator.Persistent);
        }

        public unsafe bool has(string key)
        {
            return this.board.ContainsKey(key);
        }

        public unsafe void set<T>(string key, in T value)
            where T : unmanaged
        {
            Unity.Assertions.Assert.IsTrue(sizeof(T) < FixedString128Bytes.UTF8MaxLengthInBytes, $"Type {typeof(T).Name} [size: {sizeof(T)}] exceeds maximum blackboard value capacity of {FixedString128Bytes.UTF8MaxLengthInBytes}.");

            var bytes = MemoryMarshal.Cast<T, byte>(new T[] { value });

            var _value = new FixedString128Bytes();

            fixed(byte* ptr = &bytes[0])
            {
                FixedStringMethods.Append(ref _value, ptr, bytes.Length);
                this.board[key] = _value;
            }
        }

        public unsafe T get<T>(string key)
            where T : unmanaged
        {
            T value = default;

            if(this.board.TryGetValue(key, out FixedString128Bytes bytes))
            {
                Unity.Assertions.Assert.IsTrue(sizeof(T) == bytes.Length, $"Type {typeof(T).Name} [size: {sizeof(T)}] does not match the expected value size of {bytes.Length} for blackboard key '{key}'.");
                var span = new Span<byte>(bytes.GetUnsafePtr(), bytes.Length);

                // Get the value by casting the byte span to the target type
                value = MemoryMarshal.Cast<byte, T>(span)[0];
            }

            return value;
        }

        public void clear() { this.board.Clear(); }

        public void Dispose()
        {
            UnityEngine.Debug.Log("Free blackboard");

            if(this.board.IsCreated) { this.board.Dispose(); }
        }
    }

    [AttributeUsage(AttributeTargets.Field)]
    public class BlackboardValueAttribute : Attribute
    {
        public string   name;

        public BlackboardValueAttribute(string name)
        {
            this.name = name;
        }
    }

    [Serializable]
    public struct BlackboardValue<T>
        where T : unmanaged
    {
        [NonSerialized]
        private Blackboard         blackboard;

        private string             key;

        public bool hasValue
        {
            get { return this.blackboard.has(this.key); }
        }

        public  T value
        {
            get { return this.blackboard.get<T>(this.key); }
            set { this.blackboard.set<T>(this.key, in value); }
        }
    }

    public struct Context : IDisposable
    {
        public  readonly Entity         entity;
        public  readonly EntityManager  entityManager;

        public  readonly GameObject     gameObject;
        public  readonly Sensor         perception;

        private readonly Blackboard     blackboard;

        public Context(in Entity entity, in EntityManager entityManager, Blackboard blackboard)
        {
            this.entity         = entity;
            this.entityManager  = entityManager;
            this.blackboard     = blackboard;
            this.gameObject     = entityManager.GetComponentObject<Transform>(entity)?.gameObject;

            this.perception     = entityManager.HasComponent<Sensor>(entity)
                ? entityManager.GetComponentData<Sensor>(entity)
                : Sensor.Default;
        }

        public void Dispose()
        {
        }
    }

    [CreateAssetMenu(menuName = "N")]
    public class dummy : Node
    {
        public BlackboardValue<Vector2> position;

        public override State evaluate(ref Context context)
        {
            throw new NotImplementedException();
        }
    }

    [CreateAssetMenu(menuName = "BB")]
    public class BehaviourTree : ScriptableObject, IComponentData, IDisposable
    {   
        public  Node         root;

        [NonSerialized]
        private Blackboard   blackboard;

        public BehaviourTree Instanciate()
        {
            var instance        = ScriptableObject.Instantiate<BehaviourTree>(this);
            instance.blackboard = new Blackboard();
            instance.bindBlackboardValues();

            return instance;
        }

        public void OnDestroy()
        {
            this.Dispose();    
        }

        public void Dispose()
        {
            this.blackboard.Dispose();
        }

        public void evaluate(Entity entity, EntityManager entityManager)
        {
            this.blackboard.clear();

            var context     = new Context(in entity, in entityManager, this.blackboard);
            this.root.state = this.root.evaluate(ref context);

            context.Dispose();
        }

        private static readonly Type TBlackboardValue = typeof(BlackboardValue<>);

        private void bindBlackboardValues()
        {
            if(this.root != null)
            {
                this.root.visit((Node node) =>
                {
                    var blackboardValueFields = node
                        .GetType()
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == TBlackboardValue);

                    foreach(var field in blackboardValueFields)
                    {
                        var bbValue     = field.GetValue(node);

                        // make sure blackboard value key is valid
                        var keyField    = bbValue.GetType().GetField("key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if(string.IsNullOrWhiteSpace(keyField.GetValue(bbValue) as string))
                        {
                            // if key not set yet, use key specified by BlackboardValueAttribute or fallback to field name
                            var attribute = field.GetCustomAttribute<BlackboardValueAttribute>();
                            var key = attribute != null ? attribute.name : field.Name;
                            keyField.SetValue(bbValue, key);
                        }

                        // set blackboard runtime instance
                        if(this.blackboard != null)
                        {
                            var blackbaord = bbValue.GetType().GetField("blackboard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                            blackbaord.SetValue(bbValue, this.blackboard);
                        }

                        field.SetValue(node, bbValue);
                    }
                });
            }
        }
    }

    namespace entities
    {
        [UpdateBefore(typeof(SteeringBehaviourGroup))]
        public partial class BehaviourTreeGroup : ComponentSystemGroup
        {
            internal const uint UPDATE_RATE_MS = (uint)(30.0f / 60.0f * 1000.0f);

            protected override void OnCreate()
            {
                base.OnCreate();
                this.RateManager = new RateUtils.VariableRateManager(BehaviourTreeGroup.UPDATE_RATE_MS, true);
            }
        }

        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial class UpdateBehviourTrees : SystemBase
        {
            protected override void OnCreate()
            {
                this.RequireForUpdate(StateManager.state(this));
                this.RequireForUpdate<BehaviourTree>();
            }

            protected override void OnUpdate()
            {
                foreach(var (bt, entity) in SystemAPI.Query<BehaviourTree>().WithEntityAccess())
                {
                    bt.evaluate(entity, this.EntityManager);
                }
            }
        }
    }
}
