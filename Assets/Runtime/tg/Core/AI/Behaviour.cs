using Unity.Entities;
using Unity.Transforms;

namespace tg.ai
{

    internal static class BehaviourInternal
    {
        internal static ref BehaviourContextData getBehaviourContext<T>(this IBehaviour<T> value, in Entity entity) where T : IBehaviourContext<T>
        {
            return ref World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BehaviourContextData>(entity).ElementAt(IBehaviourContext<T>.ID);
        }
    }

    internal interface IBehaviour<T> : ISystem
        where T : IBehaviourContext<T>
    {

    }

    /// <summary>
    /// Defines a common system groud where all AI context behaviour systems should be placed into.
    /// </summary>
    [UpdateAfter(typeof(TransformSystemGroup))]
    [UpdateBefore(typeof(LateSimulationSystemGroup))]
    public partial class BehaviourSystemGroup : ComponentSystemGroup {}
}
