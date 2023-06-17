using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;


    public struct Wander : IBehaviourContext<Wander>
    {
    }

    [UpdateInGroup(typeof(BehaviourSystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct WanderBehaviour : IBehaviour<Wander>
    {
        void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<WanderBehaviour>(state.SystemHandle));
        }

        void OnUpdate(ref SystemState state)
	    {
            foreach(var (wander, entity) in SystemAPI.Query<Wander>().WithEntityAccess())
            {
                ref var ctx = ref this.getBehaviourContext(entity);

                var newCtx = BehaviourContext.zero;
                
                newCtx[0] = 1.23f;

                ctx.weight = 1.0f;
                ctx.context = newCtx;
            }
        }
    }
}
