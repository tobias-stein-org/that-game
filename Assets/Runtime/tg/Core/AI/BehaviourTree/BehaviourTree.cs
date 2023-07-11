using System;
using System.Linq;
using System.Collections.Generic;
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

#if UNITY_EDITOR
        public const int initial = 0;
#endif
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
    public class BlackboardValue<T>
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

        public Context(in Entity entity, in EntityManager entityManager)
        {
            this.entity         = entity;
            this.entityManager  = entityManager;
            this.gameObject     = entityManager.GetComponentObject<Transform>(entity)?.gameObject;

            this.perception     = entityManager.HasComponent<Sensor>(entity)
                ? entityManager.GetComponentData<Sensor>(entity)
                : Sensor.Default;
        }

        public void Dispose()
        {
        }
    }

    public class BehaviourTree : ScriptableObject, IComponentData, IDisposable
    {
        public const string     label = "behaviour_tree";

        [HideInInspector]
        public  Node            root = null;

        #region Behaviour Tree Editor

        /// <summary>
        /// Contains all currently created nodes in the BT asset. These nodes do not
        /// need necessarly need to be attached to the tree itself, that is, are child
        /// of the root. We still want to keep these nodes in our final asset.
        /// </summary>
        [HideInInspector]
        public List<Node>      nodes = new List<Node>();

        #endregion

        #region RUNTIME

        [NonSerialized]
        private Blackboard      blackboard;

        public bool isCloned    { get { return this.blackboard != null; } }

        public void OnDestroy()
        {
            this.Dispose();    
        }

        public BehaviourTree clone()
        {
            var instance        = Instantiate(this);

            instance.nodes.Clear();
            instance.blackboard = new Blackboard();
            instance.root       = instance.root.clone();

            instance.root.visit(clone =>
            {
                BehaviourTree.bindBlackboardValues(clone, this.nodes.Find(node => node.id == clone.id), instance.blackboard);
                instance.nodes.Add(clone);
            });

            return instance;
        }

        public void Dispose()
        {
            this.blackboard.Dispose();
        }

        public void evaluate(Entity entity, EntityManager entityManager)
        {
            this.blackboard.clear();

            var context     = new Context(in entity, in entityManager);
            this.root.state = this.root.evaluate(ref context);

            context.Dispose();
        }

        private static readonly Type TBlackboardValue = typeof(BlackboardValue<>);

        private static void bindBlackboardValues(Node node, Node template, Blackboard blackboard)
        {
            var blackboardValueFields = node
                .GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(field => field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == TBlackboardValue);

            foreach(var field in blackboardValueFields)
            {
                if(!field.IsPublic)
                {
                    Debug.LogWarning($"Blackboard field must be declared public in order to work. Please check: '{field.DeclaringType.FullName}.{field.Name}'");
                }

                var bbValue     = field.GetValue(node);

                // make sure blackboard value key is valid
                var keyField    = bbValue.GetType().GetField("key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                var keyValue    = keyField.GetValue(field.GetValue(template)) as string;
                if(string.IsNullOrWhiteSpace(keyValue))
                {
                    // if key not set yet, use key specified by BlackboardValueAttribute or fallback to field name
                    var attr    = field.GetCustomAttribute<BlackboardValueAttribute>();
                    keyValue    = attr != null ? attr.name : field.Name;
                }

                keyField.SetValue(bbValue, keyValue);

                // set blackboard runtime instance
                var blackbaord = bbValue.GetType().GetField("blackboard", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                blackbaord.SetValue(bbValue, blackboard);

                field.SetValue(node, bbValue);
            }
        }

        #endregion
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
        [UpdateInGroup(typeof(BehaviourTreeGroup))]
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
