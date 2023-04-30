using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public partial class BarSystem : SystemBase, IEventListener
{
    protected override void OnUpdate()
    {
    }

    protected override void OnStartRunning()
    {
        EventManager.Instance.Subscribe(this);
    }

    protected override void OnStopRunning()
    {
        EventManager.Instance.Unsubscribe(this);
    }

    private void OnBarEvent(BarEvent e)
    {
        var entity = this.EntityManager.CreateEntity();
        this.EntityManager.AddComponentData<CompA>(entity, new CompA { foo = 1 });
    }
}
