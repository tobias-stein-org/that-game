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

    public partial class CameraSystem : SystemBase, IEventListener<CameraSystem>
    {
        protected override void OnCreate()
        {
        }

        protected override void OnStartRunning()
        {
            EventQueue.subscribe(this);
            this.Enabled = false;
        }


        protected override void OnDestroy()
        {
            EventQueue.unsubscribe(this);
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

