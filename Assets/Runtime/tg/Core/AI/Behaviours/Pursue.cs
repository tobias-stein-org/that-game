using System;

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

[assembly: RegisterGenericComponentType(typeof(tg.ai.behaviour.entities.Pursue))]

namespace tg.ai.behaviour
{
    using tg.ai;

    namespace entities
    {
        using tg.ai.entities;
        using tg.application.entities;

        [Serializable]
        public struct Pursue : IBehaviourContext<Pursue>
        {
            [NonSerialized]
            public float2   target;

            /// <summary>
            /// Min. distance to target before pursue stops.
            /// </summary>
            public float    minDistance;

            public static Pursue Default
            {
                get
                {
                    return new Pursue
                    {
                        minDistance = 0.8f
                    };
                }
            }
        }

        [UpdateInGroup(typeof(BehaviourSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct PursueBehaviour : IBehaviour<Pursue>
        {
            void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<PursueBehaviour>(state.SystemHandle)));
            }

            void OnUpdate (ref SystemState state)
	        {
                foreach(var (pursue, transform, entity) in SystemAPI.Query<Pursue, SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>().WithEntityAccess())
                {
                    var position            = transform.Value.position;
                    var diff                = (new Vector2(pursue.target.x, pursue.target.y) - (Vector2)position);
                    var dist                = diff.magnitude;

                    if(dist > pursue.minDistance)
                    {
                        ref var behaviour   = ref this.getBehaviourContext(entity);
                        var context         = BehaviourContext.zero;
                        var segment         = BehaviourContext.dir2seg(diff.normalized);

                        context[segment]    = math.min(dist, 1.0f);
                        behaviour.context   = context;
                    }
                    // else we have arrived
                    else
                    {
                        state.EntityManager.SetComponentEnabled<Pursue>(entity, false);
                    }
                }
            }
        }
    }
}