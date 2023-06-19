using System;

using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.ai.behaviour
{
    using tg.application;
    using tg.ai;

    [Serializable]
    public struct Pursue : IBehaviourContext<Pursue>
    {
        [NonSerialized]
        public float2   target;

        public static Pursue Default
        {
            get
            {
                return new Pursue
                {
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
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<PursueBehaviour>(state.SystemHandle));
        }

        void OnUpdate (ref SystemState state)
	    {
            foreach(var (pursue, transform, entity) in SystemAPI.Query<Pursue, SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>().WithEntityAccess())
            {
                var position            = transform.Value.position;
                var diff                = (new Vector2(pursue.target.x, pursue.target.y) - (Vector2)position);
                var dist                = diff.magnitude;

                if(dist > 1e-3f)
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