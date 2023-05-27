using Unity.Entities;
using UnityEngine;

namespace tg.test.events
{
    using tg.events;

    public struct FooData : IComponentData
    {
        public int foo;
    }

    public struct BarEvent : IEvent
    { }

    public partial struct FooSystem : ISystem, ISystemStartStop, IEventListener<FooSystem>
    {
        private int bar;

        public void OnCreate (ref SystemState state)
        {
            state.EntityManager.AddComponent<FooData>(state.SystemHandle);
            SystemAPI.SetComponent<FooData>(state.SystemHandle, new FooData { foo = 123 });
            this.bar = 1;
        }

        public void OnStartRunning(ref SystemState state)
        {
            EventQueue.subscribe(this);
        }

        public void OnStopRunning(ref SystemState state)
        {
            EventQueue.unsubscribe(this);
        }

        public void OnUpdate (ref SystemState state)
        {
            if(this.bar % 200 == 0)
            {
                EventQueue.publish<BarEvent>(new BarEvent {});
            }
        }

        private void OnIncEvent(IncEvent e)
        {
            Debug.Log("FooSystem.OnIncEvent");
            var state = World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingSystemState<FooSystem>();
            state.EntityManager.SetComponentData(state.SystemHandle, new FooData { foo = e.newValue });

            World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<FooSystem>(state.SystemHandle).bar = e.oldValue;
        }
    }
}
