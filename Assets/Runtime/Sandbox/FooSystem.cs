using Unity.Entities;
using UnityEngine;

public struct FooData : IComponentData
{
    public int foo;
}

public struct BarEvent : IEvent
{ }

public partial struct FooSystem : ISystem, ISystemStartStop, IEventListener
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
        EventManager.Instance.Subscribe(this);
    }

    public void OnStopRunning(ref SystemState state)
    {
        EventManager.Instance.Unsubscribe(this);
    }

    public void OnUpdate (ref SystemState state)
    {
        //Debug.Log(SystemAPI.GetComponent<FooData>(state.SystemHandle).foo);
        //Debug.Log(this.bar);
        //Debug.Log("===");

        if(this.bar % 200 == 0)
        {
            EventManager.Instance.Publish<BarEvent>(new BarEvent {});
        }
    }

    private void OnIncEvent(IncEvent e)
    {
        var state = World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingSystemState<FooSystem>();
        state.EntityManager.SetComponentData(state.SystemHandle, new FooData { foo = e.newValue });

        World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<FooSystem>(state.SystemHandle).bar = e.oldValue;
    }
}
