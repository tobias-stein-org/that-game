using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;

namespace tg.ai
{
    internal static class BehaviourContextInternal
    {
        /// <summary>
        /// The upper limit of how many ai behaviours there can be used during the application run-time.
        /// </summary>
        internal const int MAX_BEHAVIOURS = 8;

        /// <summary>
        /// Running sequence counter for any new derived context behaviour type.
        /// </summary>
        internal static int NEXT_BEHAVIOUR_INDEX = 0;

        internal static int nextBehaviourIndex()
        {
            Unity.Assertions.Assert.IsTrue(BehaviourContextInternal.NEXT_BEHAVIOUR_INDEX < BehaviourContextInternal.MAX_BEHAVIOURS, $"You have reached the upper limit of '{MAX_BEHAVIOURS}' AI behaviours. If you need more increase the 'BehaviourContextInternal.MAX_BEHAVIOURS' limit.");
            return BehaviourContextInternal.NEXT_BEHAVIOUR_INDEX++;
        }

        /// <summary>
        /// Adds an ID() extension method to each concrete context behaviour class. This will allow the retrievabl of the
        /// type unique index.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value"></param>
        /// <returns></returns>
        internal static int ID<T>(this IBehaviourContext<T> value) where T : IBehaviourContext<T> { return IBehaviourContext<T>.ID; }

        internal static void normailze(this FixedList64Bytes<float> value)
        {
            float maxValue = float.MinValue;
            for(int i = 0; i < value.Length; i++) { maxValue = math.max(value[i], maxValue); }
            for(int i = 0; i < value.Length; i++) { value[i] /= maxValue; }
        }

        internal static bool isValid(this FixedList64Bytes<float> value)
        {
            return value.Length != 0 && (value.Length == value.Capacity);
        }

        internal static FixedList64Bytes<float> lerp(this FixedList64Bytes<float> value0, in FixedList64Bytes<float> value1, float t)
        {
            var result = value0;
            for(int i = 0; i < result.Length; i++) { result[i] = math.lerp(value0[i], value1[i], t); }

            return result;
        }

        internal static void mul(this FixedList64Bytes<float> value, float weight)
        {
            for(int i = 0; i < value.Length; i++) { value[i] *= weight; }
        }
    }

    /// <summary>
    /// Each context aware ai behaviour has to derive from this interface.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IBehaviourContext<T> : IComponentData, IEnableableComponent
    {
        /// <summary>
        /// This will provide a unique sequence index (starting from 0) to each concrete (derived) ai context behaviour.
        /// </summary>
        internal static readonly int ID = BehaviourContextInternal.nextBehaviourIndex();
    }

    internal struct Solved : IBehaviourContext<Solved> { }


    /// <summary>
    /// Dynamic buffer component to store AI behaviour contexts.
    /// </summary>
    [InternalBufferCapacity(BehaviourContextInternal.MAX_BEHAVIOURS)]
    public struct BehaviourContext : IBufferElementData
    {
        /// <summary>
        /// Behaviour context values.
        /// </summary>
        public FixedList64Bytes<float>  context;

        /// <summary>
        /// Last frames behaviours context.
        /// </summary>
        public FixedList64Bytes<float>  context1;

        public float                    weight;
        public float                    blend;

        /// <summary>
        /// Returns an empty context (all values are 0.0), which can be filled by the behaviour.
        /// </summary>  
        public static readonly FixedList64Bytes<float> Empty;

        public static readonly FixedList64Bytes<float> Invalid;

        static BehaviourContext()
        {
             FixedList64Bytes<float> emptyContext   = new FixedList64Bytes<float>();
             emptyContext.Length                    = 15;

             BehaviourContext.Empty                 = emptyContext;
             BehaviourContext.Invalid               = new FixedList64Bytes<float>();
        }
    }
}
