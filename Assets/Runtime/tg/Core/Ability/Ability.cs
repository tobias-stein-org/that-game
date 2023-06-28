using System;

using UnityEngine;

using Unity.Jobs;
using Unity.Entities;
using Unity.Entities.Serialization;
using Unity.Collections;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace tg.ability
{
    using tg.application;
    using tg.events;
    using tg.assets;
    using tg.assets.events;
    using tg.ability.events;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AbilityPropertyAttribute : Attribute
    {

    }

    [DisallowMultipleComponent]
    public class Ability : MonoBehaviour
    {
        public Entity   caster;
        public Vector2  initialDirection;

        protected virtual void Awake()
        {
            // Do not put any ability logic inside the awake method! Use Start instead.
        }
    }

    namespace entities
    {
        [Serializable]
        public struct AbilityMeta : IComponentData
        {
            public enum Target : byte
            {
                None,
                Self,
                Point,
                Target,
                Area,
                Global
            }

            public enum Activation : byte
            {
                Instant,
                Charge,
                Channel
            }

            public  FixedString64Bytes              name;

            public  FixedString128Bytes             description;

            public  bool                            isPassive;
                                                
            public  Target                          target;
                                                
            public  Activation                      activation;
                                                
            public  float                           cost;
                                                
            public  float                           cooldown;
            public  bool                            hasCooldown { get { return this.cooldown > 0.0f; } }
                                                
            public  float                           duration;
        }

        public struct AbilityData : IComponentData
        {
            public WeakAssetReference<GameObject>  abilityPrefab;
        }

        public struct AbilityLearned : IComponentData, IEnableableComponent {}

        public struct AbilityReady : IComponentData, IEnableableComponent {}

        //public struct AbilityActive : IComponentData, IEnableableComponent
        //{
        //    public float                            activated;
        //}

        public struct AbilityCooldown : IComponentData, IEnableableComponent
        {
            public float                            cooldown;
        }

        public struct Ability : IComponentData
        {
            public Entity   owner;
            public Entity   ability;
        }


        [BurstCompile]
        [CreateAfter(typeof(EventQueue))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenInitializing, false)]
        public partial struct AbilitySystem : ISystem, IEventListener<AbilitySystem>
        {
            private struct EntityAbilityBinding : IEquatable<EntityAbilityBinding>
            {
                public readonly Entity entity;
                public readonly Entity ability;

                public EntityAbilityBinding(in Entity entity, in Entity ability)
                {
                    this.entity     = entity;
                    this.ability    = ability;
                }

                public bool Equals(EntityAbilityBinding other)
                {
                    return this.entity.Equals(other.entity) && this.ability.Equals(other.ability);   
                }

                public override int GetHashCode()
                {
                    return (int)math.hash(new int2(this.entity.Index, this.ability.Index));
                }
            }

            private ComponentLookup<AbilityData>                abilityDataLookup;
            private ComponentLookup<AbilityMeta>                abilityMetaLookup;
            private ComponentLookup<AbilityCooldown>            abilityCoolLookup;
            private ComponentLookup<AbilityReady>               abilityReadyLookup;

            private ComponentTypeHandle<AbilityCooldown>        cooldownTypeHandle;
            private ComponentTypeHandle<AbilityReady>           readyTypeHandle;

            private EntityQuery                                 updateCooldown;
            //private EntityQuery                     updateActive;

            private NativeHashMap<EntityAbilityBinding, Entity> learnedEntityAbilities;
            private NativeList<UseAbilityEvent<Entity>>         usedAbilities;


            private EntityQuery                                 abilityDataBecameAvialable;
            private NativeHashMap<FixedString64Bytes, Entity>   name2Ability;

            [BurstCompile]
            private struct UpdateCooldown : IJobChunk
            {
                public ComponentTypeHandle<AbilityCooldown> cooldownTypeHandle;
                public ComponentTypeHandle<AbilityReady>    readyTypeHandle;
                public double                               deltaTime;

                [BurstCompile]
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    NativeArray<AbilityCooldown>    cooldowns   = chunk.GetNativeArray(ref this.cooldownTypeHandle);
                    NativeArray<AbilityReady>       readies     = chunk.GetNativeArray(ref this.readyTypeHandle);

                    var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
                    while(enumerator.NextEntityIndex(out var i))
                    {
                        cooldowns[i] = new AbilityCooldown { cooldown = cooldowns[i].cooldown - (float)this.deltaTime };

                        if(cooldowns[i].cooldown <= 0.0f)
                        {
                            chunk.SetComponentEnabled<AbilityCooldown>(ref this.cooldownTypeHandle, i, false);
                            chunk.SetComponentEnabled<AbilityReady>(ref this.readyTypeHandle, i, true);
                        }
                    }
                }
            }

            //[BurstCompile]
            //private struct UpdateActive : IJobChunk
            //{
            //    public ComponentTypeHandle<Ability>         abilityTypeHandle;
            //    public ComponentTypeHandle<AbilityActive>   activeTypeHandle;
            //    public ComponentLookup<AbilityMeta>         abilityMetaLookup;

            //    public double                               deltaTime;

            //    [BurstCompile]
            //    public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            //    {
            //        NativeArray<Ability>        abilities = chunk.GetNativeArray(ref this.abilityTypeHandle);
            //        NativeArray<AbilityActive>  cooldowns = chunk.GetNativeArray(ref this.activeTypeHandle);

            //        var enumerator = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);
            //        while(enumerator.NextEntityIndex(out var i))
            //        {
            //            //cooldowns[i] = new AbilityCooldown { cooldown = cooldowns[i].cooldown - (float)this.deltaTime };

            //            //if(cooldowns[i].cooldown <= 0.0f)
            //            //{
            //            //    chunk.SetComponentEnabled<AbilityCooldown>(ref this.activeTypeHandle, i, false);
            //            //}
            //        }
            //    }
            //}

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<AbilitySystem>(state.SystemHandle)));

                this.abilityDataBecameAvialable = state.GetEntityQuery(typeof(AbilityMeta));
                this.abilityDataBecameAvialable.SetChangedVersionFilter(typeof(AbilityMeta));

                state.RequireAnyForUpdate(new EntityQuery[]
                {
                    this.abilityDataBecameAvialable,
                    state.GetEntityQuery(typeof(Ability)),
                });


                this.name2Ability               = new NativeHashMap<FixedString64Bytes, Entity>(32, Allocator.Persistent);

                this.abilityDataLookup          = state.GetComponentLookup<AbilityData>(true);
                this.abilityMetaLookup          = state.GetComponentLookup<AbilityMeta>(true);
                this.abilityCoolLookup          = state.GetComponentLookup<AbilityCooldown>(false);
                this.abilityReadyLookup         = state.GetComponentLookup<AbilityReady>(false);

                this.cooldownTypeHandle         = state.GetComponentTypeHandle<AbilityCooldown>(false);
                this.readyTypeHandle            = state.GetComponentTypeHandle<AbilityReady>(false);

                this.updateCooldown             = state.GetEntityQuery(new EntityQueryDesc
                {
                    All                         = new ComponentType[] { typeof(AbilityCooldown) },
                });

                //this.updateActive       = state.GetEntityQuery(new EntityQueryDesc
                //{
                //    All                 = new ComponentType[] { typeof(AbilityActive) },
                //});


                this.learnedEntityAbilities     = new NativeHashMap<EntityAbilityBinding, Entity>(128, Allocator.Persistent);
                this.usedAbilities              = new NativeList<UseAbilityEvent<Entity>>(128, Allocator.Persistent);

                EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<AbilitySystem>(state.SystemHandle));
            }

            public void OnDestroy(ref SystemState state)
            {
                this.learnedEntityAbilities.Dispose();
                this.usedAbilities.Dispose();
                this.name2Ability.Dispose();
            }

            public void OnUpdate(ref SystemState state)
            {
                this.abilityDataLookup.Update(ref state);
                this.abilityMetaLookup.Update(ref state);
                this.abilityCoolLookup.Update(ref state);
                this.abilityReadyLookup.Update(ref state);
                this.cooldownTypeHandle.Update(ref state);
                this.readyTypeHandle.Update(ref state);

                if(!this.abilityDataBecameAvialable.IsEmpty)
                {
                    var entity  = this.abilityDataBecameAvialable.ToEntityArray(Allocator.Temp);
                    var meta    = this.abilityDataBecameAvialable.ToComponentDataArray<AbilityMeta>(Allocator.Temp);

                    for(int i = 0; i < entity.Length; i++)
                    {
                        if(!this.name2Ability.TryAdd(meta[i].name, entity[i])) { this.name2Ability[meta[i].name] = entity[i]; }
                        Debug.Log($"Ability '{meta[i].name}' found.");
                    }
                }

                // active triggered abilities
                if(!this.usedAbilities.IsEmpty)
                {
                    var buffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged);
                    foreach(var used in this.usedAbilities)
                    {
                        var binding             = new EntityAbilityBinding(used.entity, used.ability);
                        var ability             = this.learnedEntityAbilities[binding];
                        var data                = this.abilityDataLookup[used.ability];
                        var meta                = this.abilityMetaLookup[used.ability];

                        // stop right here, if ability is not ready yet
                        if(!this.abilityReadyLookup.IsComponentEnabled(ability))
                        {
                            Debug.Log($"{used.entity}: ability '{meta.name}' is not ready yet. [{this.abilityCoolLookup.GetRefRO(ability).ValueRO.cooldown}]");
                            continue;
                        }

                        spawn.request.create(data.abilityPrefab, new Vector3(used.point.x, used.point.y, 0f), 0f, (GameObject abilityGO) =>
                        {
                            var ability = abilityGO.GetComponentInChildren<tg.ability.Ability>() ?? abilityGO.AddComponent<tg.ability.Ability>();
                            {
                                ability.caster              = used.entity;
                                ability.initialDirection    = used.direction;
                            }
                        });

                        // update cooldown and ready state
                        if(meta.hasCooldown)
                        {
                            buffer.SetComponent<AbilityCooldown>(ability, new AbilityCooldown
                            {
                                cooldown = meta.cooldown
                            });
                            buffer.SetComponentEnabled<AbilityCooldown>(ability, true);
                            buffer.SetComponentEnabled<AbilityReady>(ability, false);
                        }

                        Debug.Log($"{used.entity}: used ability '{meta.name}'.");
                    }
                    this.usedAbilities.Clear();
                }

                this.cooldownTypeHandle.Update(ref state);
                this.readyTypeHandle.Update(ref state);

                var updateCooldown              = new UpdateCooldown
                {                               
                    cooldownTypeHandle          = this.cooldownTypeHandle,
                    readyTypeHandle             = this.readyTypeHandle,
                    deltaTime                   = state.World.Time.DeltaTime
                };                              
                                            
                state.Dependency                = updateCooldown.ScheduleParallel(this.updateCooldown, state.Dependency);
            }

            void onUseAbilityEvent(UseAbilityEvent<Entity> e) { this.usedAbilities.Add(e); }
            void onUseAbilityEvent(UseAbilityEvent<FixedString64Bytes> e)
            {
                this.usedAbilities.Add(new UseAbilityEvent<Entity>
                {
                    ability                         = this.name2Ability[e.ability],
                    entity                          = e.entity,
                    point                           = e.point,
                    direction                       = e.direction
                });
            }

            void onLearnAbilityEvent(LearnAbilityEvent<FixedString64Bytes> e)
            {
                if(this.name2Ability.TryGetValue(e.ability, out Entity ability))
                {
                    this.onLearnAbilityEvent(new LearnAbilityEvent<Entity> { ability = ability, entity = e.entity });
                }
                else
                {
                    Debug.LogError($"{e}: wants to learn {e.ability}, but it does not exist.");
                }
            }

            void onLearnAbilityEvent(LearnAbilityEvent<Entity> e)
            {
                var entityManager       = World.DefaultGameObjectInjectionWorld.EntityManager;

                if(!entityManager.IsComponentEnabled<AbilityLearned>(e.ability))
                {
                    var data            = entityManager.GetComponentData<AbilityData>(e.ability);
                    var self            = this;
                    EventQueue.publish(new RequestLoadAssetsEvent
                    {
                        assets          = new NativeArray<UntypedWeakReferenceId>( new UntypedWeakReferenceId[] { data.abilityPrefab }, Allocator.Persistent),
                        onComplete      = (bool hasErrors) =>
                        {
                            if(!hasErrors)
                            {
                                self.doLearnAbility(entityManager, e.entity, e.ability);
                            }
                        }
                    });

                    // note: we have to enable the state right away, otherwise we might run into the problem of loading the ability twice
                    entityManager.SetComponentEnabled<AbilityLearned>(e.ability, true);
                }
                else
                {
                    this.doLearnAbility(entityManager, e.entity, e.ability);
                }
            }

            private void doLearnAbility(in EntityManager entityManager, in Entity entity, in Entity ability) 
            {
                var entityAbility   = entityManager.CreateEntity(new ComponentType[]
                {
                    typeof(Ability),
                    typeof(AbilityReady),
                    //typeof(AbilityActive),
                    typeof(AbilityCooldown)
                });

    #if UNITY_EDITOR
                var entityName  = entityManager.GetName(entity);
                entityName      = string.IsNullOrEmpty(entityName) ? entity.ToString() : entityName;

                var meta        = entityManager.GetComponentData<AbilityMeta>(ability);

                entityManager.SetName(entityAbility, $"{meta.name}-{entityName}");
                Debug.Log($"'{entityName}' learned '{meta.name}' ability.");
    #endif

                entityManager.SetComponentData<Ability>(entityAbility, new Ability
                {
                    owner           = entity,
                    ability         = ability
                });
                entityManager.SetComponentEnabled<AbilityReady>(entityAbility, true);
                //entityManager.SetComponentEnabled<AbilityActive>(abilityEntity, false);
                entityManager.SetComponentEnabled<AbilityCooldown>(entityAbility, false);

                this.learnedEntityAbilities.Add(new EntityAbilityBinding(entity, ability), entityAbility);
            }
        }
    }
}
