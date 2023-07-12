using System;

using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(tg.ai.steering.behaviour.entities.Avoid))]

namespace tg.ai.steering.behaviour
{
    using tg.ai.entities;
    using tg.ai.steering;
    using tg.application;

    namespace entities
    {
        using tg.ai.steering.entities;
        using tg.application.entities;

        [Serializable]
        public struct Avoid : IBehaviourContext<Avoid>
        {
            public float range;

            public static Avoid Default
            {
                get
                {
                    return new Avoid
                    {
                        range = 2.0f
                    };
                }
            }
        }

        [UpdateInGroup(typeof(SteeringBehaviourGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct AvoidBehaviour : IBehaviour<Avoid>
        {
            void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<AvoidBehaviour>(state.SystemHandle)));
            }


            void OnUpdate (ref SystemState state)
	        {
                foreach(var (avoid, sensor, entity) in SystemAPI.Query<Avoid, RefRO<Sensor>>().WithEntityAccess())
                {
                    ref var behaviour               = ref this.getBehaviourContext(entity);

                    // initialize context to prefer any direction
                    var context                     = BehaviourContext.zero;

                    // check for level collision
                    foreach(var perception in sensor.ValueRO.query().withTag(tags.Level).withInRange(avoid.range))
                    {
                        context[perception.segment] = -1.0f + (perception.distance / avoid.range); // [-1.0; 0.0]
                    }

                    behaviour.context               = context;
                }
            }
        }
    }

}