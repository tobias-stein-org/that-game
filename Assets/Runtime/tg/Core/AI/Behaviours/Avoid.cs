using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;


    public struct Avoid : IBehaviourContext<Avoid>
    {
    }

    [UpdateBefore(typeof(BehaviourSolver))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct AvoidBehaviour : IBehaviour<Avoid>
    {
        void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<AvoidBehaviour>(state.SystemHandle));
        }

        void OnUpdate (ref SystemState state)
	    {
            foreach(var (pursue, entity) in SystemAPI.Query<Avoid>().WithEntityAccess())
            {
                ref var ctx = ref this.getBehaviourContext(entity);

                var newCtx = BehaviourContext.zero;

                newCtx[0] = 1.23f;
                newCtx[1] = -1.23f;

                ctx.context = newCtx;
            }
        }
    }
}