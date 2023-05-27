using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace tg.test.events
{
    using tg.events;

    public partial class BarSystem : SystemBase, IEventListener<BarSystem>
    {
        protected override void OnUpdate()
        {
        }

        protected override void OnStartRunning()
        {
            EventQueue.subscribe(this);
        }

        protected override void OnStopRunning()
        {
            EventQueue.unsubscribe(this);
        }

        private void OnBarEvent(BarEvent e)
        {
            Debug.Log("BarSystem.OnBarEvent");
            var entity = this.EntityManager.CreateEntity();
            this.EntityManager.AddComponentData<CompA>(entity, new CompA { foo = 1 });
        }
    }
}
