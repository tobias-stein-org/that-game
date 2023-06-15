using UnityEngine;
using Unity.Entities; 

namespace tg.enemy {

    using tg.application;
    using tg.events;
    using tg.enemy.events;
    using tg.spawn.events;
    using tg.player.events;
    using tg.player;
    using tg.ai;
    using tg.ai.behaviour;

    /// <summary>
    /// Added to spawned enemy entities.
    /// </summary>
    public struct Enemy : IComponentData
    {
        /// <summary>
        /// Reference to actual enemy entity.
        /// </summary>
        public Entity                   entity;
    }

    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct EnemyManager : ISystem, IEventListener<EnemyManager>, ISystemStartStop
    {
        void OnCreate(ref SystemState state)
		{
            state.RequireForUpdate(StateManager.state(this));
		}

		void OnDestroy(ref SystemState state)
		{
            this.killAllEnemies();
		}

        public void OnStartRunning(ref SystemState state)
        {
            EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<EnemyManager>(state.SystemHandle));

        }

        public void OnStopRunning(ref SystemState state)
        {
            EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<EnemyManager>(state.SystemHandle));

        }

        void onSpawnEnemyRequestEvent(SpawnEnemyRequestEvent e)
        {
            var appData = SystemAPI.GetSingleton<ApplicationData>();

            for(int i = 0; i < e.amount; i++)
            {
                tg.spawn.request.create(appData.enemyPrefab, in e.desc.location, 0, (GameObject instance) =>
                {
                    EnemyManager.createEnemyEntity(instance, e.desc);
                });
            }
        }

        void onEntitySpawnedEvent(EntitySpawnedEvent e)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(entityManager.HasComponent<Enemy>(e.entity))
            {
                EventQueue.publish(new EnemySpawnedEvent { enemy = entityManager.GetComponentData<Enemy>(e.entity) });
            }
        }

        void onKillPlayerEvent(KillEnemyEvent e)
        {
            var entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;
            var playerGO        = entityManager.GetComponentObject<Transform>(e.enemy.entity).gameObject;

            GameObject.Destroy(playerGO);
            entityManager.DestroyEntity(e.enemy.entity);

            EventQueue.publish(new EnemyDiedEvent { enemy = e.enemy });
        }

        private static void createEnemyEntity(GameObject instance, SpawnRequestDescription description)
        {
            var enemyEntity = tg.spawn.request.create(out EntityCommandBuffer ECB);
            {
                ECB.AddComponent(enemyEntity, new ComponentTypeSet(
                   typeof(Enemy),
                   typeof(BehaviourContextData)
                ));

                ECB.SetComponent<Enemy>(enemyEntity, new Enemy
                {
                    entity      = enemyEntity
                });

                var buffer      = ECB.SetBuffer<BehaviourContextData>(enemyEntity);
                buffer.Length = BehaviourContextInternal.MAX_BEHAVIOURS;
                for(int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = BehaviourContextData.Default;
                }

                ECB.AddComponent<Avoid>(enemyEntity);
                ECB.AddComponent<Wander>(enemyEntity);
                //ECB.SetComponentEnabled<Wander>(enemyEntity, false);
                
                ECB.AddComponent(enemyEntity, instance.transform);
                ECB.AddComponent(enemyEntity, instance.GetComponent<Rigidbody2D>());
                ECB.AddComponent(enemyEntity, instance.GetComponentInChildren<Animator>());
            }
        }

        private void killAllEnemies()
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            entityManager.DestroyEntity(entityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Enemy) } }));
        }
    }
}
