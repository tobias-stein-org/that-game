using System;
using Unity.Entities;

namespace tg.ai.behaviour
{
    using tg.application.entities;
    using tg.ai;

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

    [UpdateInGroup(typeof(BehaviourSystemGroup))]
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