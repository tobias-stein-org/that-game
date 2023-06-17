using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;


    public struct Avoid : IBehaviourContext<Avoid>
    {
    }

    [UpdateInGroup(typeof(BehaviourSystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct AvoidBehaviour : IBehaviour<Avoid>
    {
        void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<AvoidBehaviour>(state.SystemHandle));
        }

        void OnUpdate (ref SystemState state)
	    {
            foreach(var (pursue, sensor, entity) in SystemAPI.Query<Avoid, RefRO<Sensor>>().WithEntityAccess())
            {
                ref var behaviour = ref this.getBehaviourContext(entity);

                var context = BehaviourContext.zero;

                behaviour.context = context;
            }
        }
    }
}