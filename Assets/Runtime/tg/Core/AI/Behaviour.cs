using Unity.Entities;

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
}
