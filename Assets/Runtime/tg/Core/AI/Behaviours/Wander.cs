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
        public float2           steeringRadius;

        public float            maxWanderRange;

        [NonSerialized]
        public float2           steeringForce;

        [NonSerialized]
        public float2           spawnPoint;

        public static Wander    Default
        {
            get
            {
                return new Wander
                {
                    steeringOffset      = 3.5f,
                    steeringRadius      = new float2(0.1f, 1.0f),
                    maxWanderRange      = 0.0f,
                    steeringForce       = float2.zero,
                    spawnPoint          = float2.zero
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
                var speed                       = velocity.magnitude;
                var forward                     = new Vector2(wander.ValueRO.steeringForce.x, wander.ValueRO.steeringForce.y).normalized;
                var theta                       = math.PI * UnityEngine.Random.Range(-1.0f, 1.0f);
                var wanderShapeHeading          = new Vector2(math.cos(theta), math.sin(theta));

                /*
                                                wanderShapeHeading
                                                         ^
                  p <-- wanderShapeOffset --> w          |
                                | <--           wanderShapeRadius            --> |
                 [E]-> forward
                 
                 */
                var steeringForce                   = (forward * wander.ValueRO.steeringOffset) + (wanderShapeHeading * math.lerp(wander.ValueRO.steeringRadius.y, wander.ValueRO.steeringRadius.x, speed));

                

                wander.ValueRW.steeringForce        = steeringForce;
                var wanderForceN                    = math.normalize(wander.ValueRO.steeringForce);


                ref var behaviour                   = ref this.getBehaviourContext(entity);
                {
                    var context                     = BehaviourContext.zero;
                    var segmentWander               = BehaviourContext.dir2seg(wanderForceN);
                    //context[segmentWander]          = 1.0f;
                    var dot                         = math.abs(Vector2.Dot(wanderForceN, forward));
                    context[segmentWander]          = dot * dot;

                    if(wander.ValueRO.maxWanderRange > 1e-5f)
                    {
                        var spawnPoint              = new Vector2(wander.ValueRO.spawnPoint.x, wander.ValueRO.spawnPoint.y);

                        // draw gravity pull to spawn point
                        var diffSpawn               = spawnPoint - (Vector2)position;
                        var distance                = diffSpawn.magnitude;
                        var pull                    = math.clamp(distance / math.max(wander.ValueRO.maxWanderRange, 1.0f), 0.0f, 1.0f);
                        var segmentReturn           = BehaviourContext.dir2seg(diffSpawn.normalized);

                        context[segmentReturn]      = pull * pull;
                    }

                    behaviour.context           = context;
                }
            }
        }
    }
}
