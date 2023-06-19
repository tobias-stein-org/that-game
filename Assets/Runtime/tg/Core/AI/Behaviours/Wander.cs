using System;

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;

    [Serializable]
    public struct Wander : IBehaviourContext<Wander>
    {
        public float            steeringOffset;
        public float            steeringRadius;

        [NonSerialized]
        public float2           steeringForce;

        public static Wander    Default
        {
            get
            {
                return new Wander
                {
                    steeringOffset  = 2.5f,
                    steeringRadius  = 1.5f,
                    steeringForce   = float2.zero
                };
            }
        }
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
            foreach(var (wander, rb, entity) in SystemAPI.Query<RefRW<Wander>, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>().WithEntityAccess())
            {
                var position                    = rb.Value.position;
                var velocity                    = rb.Value.velocity;
                var forward                     = velocity.normalized;

                var theta                       = math.PI *
                    // [-1.0; +1.0]
                    //UnityEngine.Random.Range(-1.0f, 1.0f);
                    noise.snoise(position);
                    //noise.snoise(velocity);
                    //noise.snoise(forward);

                var wanderShapeHeading          = new Vector2(math.cos(theta), math.sin(theta));

                /*
                                                wanderShapeHeading
                                                         ^
                  p <-- wanderShapeOffset --> w          |
                                | <--           wanderShapeRadius            --> |
                 [E]-> forward
                 
                 */
                wander.ValueRW.steeringForce    = (forward * wander.ValueRO.steeringOffset) + (wanderShapeHeading * wander.ValueRO.steeringRadius);
                var wanderForceN                = math.normalize(wander.ValueRO.steeringForce);

                ref var behaviour               = ref this.getBehaviourContext(entity);
                {
                    var context                 = BehaviourContext.zero;
                    var segment                 = BehaviourContext.dir2seg(wanderForceN);
                    context[segment]            = 1.0f - math.abs(Vector2.Dot(wanderForceN, forward));
                    behaviour.context           = context;
                }
            }
        }
    }
}
