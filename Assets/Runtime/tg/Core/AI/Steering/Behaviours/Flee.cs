using System;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(tg.ai.steering.behaviour.entities.Flee))]

namespace tg.ai.steering.behaviour
{
    namespace entities
    {
        using tg.ai.steering.entities;
        using tg.application.entities;

        [Serializable]
        public struct Flee : IBehaviourContext<Flee>
        {
            [NonSerialized]
            public int dummy;

            public static Flee Default
            {
                get
                {
                    return new Flee
                    {
                    };
                }
            }
        }

        [UpdateInGroup(typeof(SteeringBehaviourGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct FleeBehaviour : IBehaviour<Flee>
        {
            void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<FleeBehaviour>(state.SystemHandle)));
            }

            void OnUpdate (ref SystemState state)
	        {
                foreach(var (flee, entity) in SystemAPI.Query<Flee>().WithEntityAccess())
                {
                    ref var ctx = ref this.getBehaviourContext(entity);
                }
            }
        }
    }
}