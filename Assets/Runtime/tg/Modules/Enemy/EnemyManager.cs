using UnityEngine;
using Unity.Entities; 
using Unity.Mathematics;

namespace tg.enemy {

    using tg.debug;
    using tg.events;
    using tg.enemy.events;
    using tg.combat.events;
    using tg.spawn.events;
    using tg.ai.steering.entities;
    using tg.ability.events;

    namespace entities
    {
        using tg.ai.entities;
        using tg.ai.steering.behaviour.entities;
        using tg.ai.behaviour.tree;

        using tg.application.entities;
        using tg.physics.entities;
        using tg.combat.entities;

        /// <summary>
        /// Added to spawned enemy entities.
        /// </summary>
        public struct Enemy : IComponentData
        {
        }

        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenLoading, false)]
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
                var appData = SystemAPI.ManagedAPI.GetSingleton<ApplicationData>();

                for(int i = 0; i < e.amount; i++)
                {
                    tg.spawn.request.create(appData.enemyPrefab, in e.desc.location, 0, (GameObject instance) =>
                    {
                        EnemyManager.createEnemyEntity(instance, e.desc);
                    });
                }
            }

            private static void createEnemyEntity(GameObject instance, SpawnRequestDescription description)
            {
                var enemyEntity = tg.spawn.request.create(out EntityCommandBuffer ECB, default, 0f, EnemyManager.learnAbilities);
                {
                    ECB.AddComponent(enemyEntity, new ComponentTypeSet(new ComponentType[]
                    {
                       typeof(Enemy),
                       typeof(EnemyInputData),
                       typeof(BehaviourContextData),
                       typeof(Sensor),
                       typeof(Health),
                       typeof(Damagaeble),
                       typeof(Stats),
                       typeof(WithManagedCollider)
                    }));

                    ECB.SetComponent<Stats>(enemyEntity, new Stats
                    {
                        STR = 1,
                        AGI = 1,
                        INT = 1,
                        ATT = 1,
                        DEF = 1
                    });

                    ECB.SetComponent<Health>(enemyEntity, new Health
                    {
                        maxHealth       = 100f,
                        health          = 100f
                    });

                    ECB.SetComponent<EnemyInputData>(enemyEntity, new EnemyInputData
                    {
                        moveSpeed       = 2.0f,
                        turnSpeed       = 4 * 360.0f,
                        move            = float2.zero,
                        look            = new float2(-1.0f, 0.0f)
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

                    var bt = UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<BehaviourTree>("tg.ai.behaviour.tree.Simple_Enemy_BT").WaitForCompletion().clone();
                    ECB.AddComponent<BehaviourTree>(enemyEntity, bt);
                }
            }

            //private static BehaviourTree createBehaviourTree()
            //{

            //}

            private static void learnAbilities(Entity entity)
            {
                EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = entity, ability = "MELEE_ATT" });
                EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = entity, ability = "FIREBALL_1" });
            }

            void onEntitySpawnedEvent(EntitySpawnedEvent e)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if(entityManager.HasComponent<Enemy>(e.entity))
                {
    #if UNITY_EDITOR
                    var enemyDebugging      = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<Transform>(e.entity).gameObject.AddComponent<EnemyDebugging>();
                    {
                        enemyDebugging.self = e.entity;
                        enemyDebugging.behaviourTree = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<BehaviourTree>(e.entity);
                    }
    #endif

                    EventQueue.publish(new EnemySpawnedEvent { enemy = e.entity });
                }
            }

            void onEntityDiedEvent(EntityDiedEvent e)
            {
                var entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;
                if(entityManager.HasComponent<Enemy>(e.entity))
                {
                    var gameObject  = entityManager.GetComponentObject<Transform>(e.entity).gameObject;

                    GameObject.Destroy(gameObject);
                    entityManager.DestroyEntity(e.entity);
                }
            }

            void onKillEnemyEvent(KillEnemyEvent e)
            {
                var entityManager   = World.DefaultGameObjectInjectionWorld.EntityManager;
                var playerGO        = entityManager.GetComponentObject<Transform>(e.enemy).gameObject;

                GameObject.Destroy(playerGO);
                entityManager.DestroyEntity(e.enemy);
            }

        
            private void killAllEnemies()
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                entityManager.DestroyEntity(entityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Enemy) } }));
            }
        }
    }
}
