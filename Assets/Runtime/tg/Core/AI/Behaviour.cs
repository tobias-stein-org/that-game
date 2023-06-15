using Unity.Entities;

namespace tg.ai
{

    internal static class BehaviourInternal
    {
        internal static ref BehaviourContext getBehaviourContext<T>(this IBehaviour<T> value, in Entity entity) where T : IBehaviourContext<T>
        {
            return ref World.DefaultGameObjectInjectionWorld.EntityManager.GetBuffer<BehaviourContext>(entity).ElementAt(IBehaviourContext<T>.ID);
        }
    }

    internal interface IBehaviour<T> : ISystem
        where T : IBehaviourContext<T>
    {

    }
}
