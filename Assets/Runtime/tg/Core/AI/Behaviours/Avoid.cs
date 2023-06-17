using Unity.Entities;
using Unity.Mathematics;

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
            foreach(var (avoid, sensor, entity) in SystemAPI.Query<Avoid, RefRO<Sensor>>().WithEntityAccess())
            {
                var avoidRange      = 5.0f;
                ref var behaviour   = ref this.getBehaviourContext(entity);

                // initialize context to prefer any direction
                var context = BehaviourContext.zero;

                // check for level collision
                foreach(var perception in sensor.ValueRO.query().withTag(tags.Level).withInRange(avoidRange))
                {
                    var avoidance                               = -1.0f + (perception.distance / avoidRange);
                    context[perception.segment]                 = avoidance;
                    context[(perception.segment + 8) % 16]      = -avoidance;
                }

                behaviour.context = context.blur();
            }
        }
    }
}