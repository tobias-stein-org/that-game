using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;


    public struct Flee : IBehaviourContext<Flee>
    {
    }

    [UpdateInGroup(typeof(BehaviourSystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct FleeBehaviour : IBehaviour<Flee>
    {
        void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<FleeBehaviour>(state.SystemHandle));
        }

        void OnUpdate (ref SystemState state)
	    {
            foreach(var (flee, entity) in SystemAPI.Query<Flee>().WithEntityAccess())
            {
                ref var ctx = ref this.getBehaviourContext(entity);
            }
        }
    }
}