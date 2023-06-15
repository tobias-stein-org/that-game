using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;


    public struct Pursue : IBehaviourContext<Pursue>
    {
    }

    [UpdateBefore(typeof(BehaviourSolver))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct PursueBehaviour : IBehaviour<Pursue>
    {
        void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<PursueBehaviour>(state.SystemHandle));
        }

        void OnUpdate (ref SystemState state)
	    {
            foreach(var (pursue, entity) in SystemAPI.Query<Pursue>().WithEntityAccess())
            {
                ref var ctx = ref this.getBehaviourContext(entity);
            }
        }
    }
}