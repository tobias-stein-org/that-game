using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

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
            foreach(var (wander, rb, entity) in SystemAPI.Query<Wander, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>().WithEntityAccess())
            {
                var position            = rb.Value.position;
                var velocity            = rb.Value.velocity;
                var forward             = velocity.sqrMagnitude > 1e-5f ? velocity.normalized : Vector2.right;

                var wanderShapeOffset   = 1.0f;
                var wanderShapeRadius   = 1.0f;
                ref var behaviour       = ref this.getBehaviourContext(entity);

                var theta               = noise.snoise(position); // [-1.0; +1.0]
                var wanderShapeHeading  = new Vector2(math.cos(theta), math.sin(theta));

                /*
                                                wanderShapeHeading
                                                         ^
                  p <-- wanderShapeOffset --> w          |
                                | <--           wanderShapeRadius            --> |
                 [E]-> forward
                 
                 */
                var wanderForce         = (position + (forward * wanderShapeOffset) + (wanderShapeHeading * wanderShapeRadius)).normalized;
                
                var context             = BehaviourContext.zero;
                var segment             = BehaviourContext.dir2seg(wanderForce);
                context[segment]        = 1.0f - math.abs(Vector2.Dot(wanderForce, forward));
                behaviour.context       = context;
            }
        }
    }
}
