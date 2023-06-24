using UnityEngine;
using Unity.Entities; 
using Unity.Mathematics;

namespace tg.enemy {

    using tg.application;
    using tg.debug;
    using tg.events;
    using tg.enemy.events;
    using tg.spawn.events;
    using tg.player.events;
    using tg.player;
    using tg.ai;
    using tg.ai.behaviour;
    using tg.physics.entities;

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
                var enemy               = entityManager.GetComponentData<Enemy>(e.entity);
#if UNITY_EDITOR
                var enemyDebugging      = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<Transform>(e.entity).gameObject.AddComponent<EnemyDebugging>();
                {
                    enemyDebugging.self = enemy;
                }
#endif

                EventQueue.publish(new EnemySpawnedEvent { enemy = enemy });
            }
        }

        void onKillEnemyEvent(KillEnemyEvent e)
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
                   typeof(EnemyInputData),
                   typeof(BehaviourContextData),
                   typeof(Sensor),
                   typeof(WithManagedCollider)
                ));

                ECB.SetComponent<Enemy>(enemyEntity, new Enemy
                {
                    entity          = enemyEntity
                });
                ECB.SetComponent<EnemyInputData>(enemyEntity, new EnemyInputData
                {
                    lastBehaviourContextDecision = 0
                });
                ECB.SetComponent<Sensor>(enemyEntity, new Sensor(new Sensor.Description
                {
                    range           = description.behaviour.perceptionRange,
                    maxOutouts      = description.behaviour.maxPerceptions,
                    allowSeeHidden  = description.behaviour.allowSeeHidden,
                    sensorMask      = description.behaviour.filter
                }));
                ECB.SetComponent<WithManagedCollider>(enemyEntity, new WithManagedCollider
                {
                    entity          = enemyEntity,
                    gameOb          = instance
                });

                var buffer          = ECB.SetBuffer<BehaviourContextData>(enemyEntity);
                buffer.Length       = BehaviourContextInternal.MAX_BEHAVIOURS;
                for(int i           = 0; i < buffer.Length; i++)
                {
                    buffer[i]       = BehaviourContextData.Default;
                }

                // set enemy behaviour
                if(description.behaviour != null)
                {
                    var behaviour   = description.behaviour;

                    buffer[IBehaviourContext<Solved>.ID]        = new BehaviourContextData(behaviour.contextBlurring, behaviour.frameBlending);

                    if((behaviour.activeBehaviour & EnemyBehaviour.BehaviourMask.Wander) != 0)
                    {
                        buffer[IBehaviourContext<Wander>.ID]    = new BehaviourContextData(behaviour.wanderWeight, behaviour.wanderBlend);
                        behaviour.wander.spawnPoint             = description.location.xy;
                        ECB.AddComponent<Wander>(enemyEntity, behaviour.wander);
                        ECB.SetComponentEnabled<Wander>(enemyEntity, behaviour.wanderEnabled);
                    }

                    if((behaviour.activeBehaviour & EnemyBehaviour.BehaviourMask.Avoid) != 0)
                    {
                        buffer[IBehaviourContext<Avoid>.ID]     = new BehaviourContextData(behaviour.avoidWeight, behaviour.avoidBlend);
                        ECB.AddComponent<Avoid>(enemyEntity, behaviour.avoid);
                        ECB.SetComponentEnabled<Avoid>(enemyEntity, behaviour.avoidEnabled);
                    }

                    if((behaviour.activeBehaviour & EnemyBehaviour.BehaviourMask.Flee) != 0)
                    {
                        buffer[IBehaviourContext<Flee>.ID]      = new BehaviourContextData(behaviour.fleeWeight, behaviour.fleeBlend);
                        ECB.AddComponent<Flee>(enemyEntity, behaviour.flee);
                        ECB.SetComponentEnabled<Flee>(enemyEntity, behaviour.fleeEnabled);
                    }

                    if((behaviour.activeBehaviour & EnemyBehaviour.BehaviourMask.Wander) != 0)
                    {
                        buffer[IBehaviourContext<Pursue>.ID]    = new BehaviourContextData(behaviour.pursueWeight, behaviour.pursueBlend);
                        ECB.AddComponent<Pursue>(enemyEntity, behaviour.pursue);
                        ECB.SetComponentEnabled<Pursue>(enemyEntity, behaviour.pursueEnabled);
                    }
                }
                
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
