using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace tg.camera
{
    using tg.events;
    using tg.level;
    using tg.player;
    using tg.player.events;
    using tg.spawn.events;

    [CreateAfter(typeof(EventQueue))]
    public partial class CameraSystem : SystemBase, IEventListener<CameraSystem>
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate<Player>();
            EventQueue.subscribe(this);
        }

        protected override void OnStopRunning()
        {
            EventQueue.unsubscribe(this);
        }

        protected override void OnDestroy()
        {
        }

        protected override void OnUpdate()
        {
        }

        void onEntitySpawnedEvent(EntitySpawnedEvent e)
        {
            if(SystemAPI.HasComponent<Player>(e.entity))
            {
                Debug.Log("player was spawned");
            }
        }
    }
}

