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
    using tg.combat.events;

    namespace entities
    {
        using tg.application.entities;
        using tg.physics.entities;
        using tg.combat.entities;

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
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenLoading, false)]
        public partial struct PlayerManager : ISystem, IEventListener, ISystemStartStop
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
                    ECB.AddComponent(playerEntity, new ComponentTypeSet(new ComponentType[]
                    {
                       typeof(Player),
                       typeof(PlayerInputData),
                       typeof(Health),
                       typeof(Damagaeble),
                       typeof(Stats),
                       typeof(WithManagedCollider)
                    }));

                    ECB.SetComponent<Stats>(playerEntity, new Stats
                    {
                        STR = 1,
                        AGI = 1,
                        INT = 1,
                        ATT = 1,
                        DEF = 1
                    });

                    ECB.SetComponent<Health>(playerEntity, new Health
                    {
                        maxHealth   = 100f,
                        health      = 100f
                    });

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

