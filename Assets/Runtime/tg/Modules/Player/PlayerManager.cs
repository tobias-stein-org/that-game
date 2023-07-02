using Cinemachine;
using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;

namespace tg.player
{
    using tg.events;

    using tg.level;
    using tg.player.events;
    using tg.spawn.events;

    namespace entities
    {
        using tg.application.entities;
        using tg.physics.entities;

        /// <summary>
        /// Added to spawned player entities.
        /// </summary>
        public struct Player : IComponentData
        {
        }

        /// <summary>
        /// Simple player spawn system. 
        /// </summary>
        [CreateAfter(typeof(EventQueue))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct PlayerManager : ISystem, IEventListener<PlayerManager>, ISystemStartStop
        {
            void OnCreate(ref SystemState state)
		    {
                state.RequireForUpdate(StateManager.state(this));
                state.RequireForUpdate<LevelData>();
		    }

		    void OnDestroy(ref SystemState state)
		    {
                state.EntityManager.DestroyEntity(state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Player) } }));
		    }

            void onSpawnPlayerRequestEvent(SpawnPlayerRequestEvent e)
            {
                var appData = SystemAPI.ManagedAPI.GetSingleton<ApplicationData>();
                tg.spawn.request.create(appData.playerPrefab, in e.location, 0, this.createPlayerEntity);
            }

            void onEntitySpawnedEvent(EntitySpawnedEvent e)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if(entityManager.HasComponent<Player>(e.entity))
                {
                    EventQueue.publish(new PlayerSpawnedEvent { player = e.entity });
                }
            }

            void onKillPlayerEvent(KillPlayerEvent e)
            {
                if(!World.DefaultGameObjectInjectionWorld.IsCreated) { return; }

                var entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;

                var playerGO        = entityManager.GetComponentObject<Transform>(e.player).gameObject;

                GameObject.Destroy(playerGO);
                entityManager.DestroyEntity(e.player);

                EventQueue.publish(new PlayerDiedEvent { player = e.player });
            }

            public void OnStartRunning(ref SystemState state)
            {
                EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerManager>(state.SystemHandle));
            }

            public void OnStopRunning(ref SystemState state)
            {
                EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerManager>(state.SystemHandle));
            }

            private void createPlayerEntity(GameObject instance)
            {
                var playerEntity = tg.spawn.request.create(out EntityCommandBuffer ECB);
                {
    #if UNITY_EDITOR
                    ECB.SetName(playerEntity, instance.name);
    #endif
                    ECB.AddComponent(playerEntity, new ComponentTypeSet(
                       typeof(Player),
                       typeof(PlayerInputData),
                       typeof(WithManagedCollider)
                    ));

                    ECB.SetComponent<PlayerInputData>(playerEntity, new PlayerInputData
                    {
                        moveSpeed   = 5.0f,
                        turnSpeed   = 4 * 360.0f,
                        move        = float2.zero,
                        look        = new float2(-1.0f, 0.0f)
                    });

                    ECB.SetComponent<WithManagedCollider>(playerEntity, new WithManagedCollider
                    {
                        entity      = playerEntity,
                        gameOb      = instance
                    });

                    ECB.AddComponent(playerEntity, instance.transform);
                    ECB.AddComponent(playerEntity, instance.GetComponent<Rigidbody2D>());
                    ECB.AddComponent(playerEntity, instance.GetComponentInChildren<Animator>());
                    ECB.AddComponent(playerEntity, instance.GetComponentInChildren<CinemachineVirtualCamera>());
                }
            }
        }
    }
}

