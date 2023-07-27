using System;

using UnityEngine;
using UnityEngine.AddressableAssets;

using Unity.Jobs;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace tg.ability
{
    using tg.events;
    using tg.combat;

    using tg.game.events;
    using tg.ability.events;
    using tg.combat.events;

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

        protected void dealDamage(in Entity target, ref Damage damage)
        {
            damage.source   = this.caster;

            EventQueue.publish(new DealDamageEvent
            {
                source      = this.caster,
                target      = target,
                damage      = damage
            });
        }
    }

    namespace entities
    {
        using tg.application.entities;

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
            public int                              abilityPrefabID;
        }

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
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenLoading, false)]
        public partial struct UpdateAbilityCooldown : ISystem
        {
            [BurstCompile]
            private struct UpdateCooldown : IJobChunk
            {
                public ComponentTypeHandle<AbilityCooldown> cooldownTypeHandle;
                public ComponentTypeHandle<AbilityReady>    readyTypeHandle;
                public double                               deltaTime;

                [BurstCompile]
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    var cooldowns       = chunk.GetNativeArray(ref this.cooldownTypeHandle);
                    var enumerator      = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                    while(enumerator.NextEntityIndex(out var i))
                    {
                        cooldowns[i]    = new AbilityCooldown { cooldown = cooldowns[i].cooldown - (float)this.deltaTime };

                        if(cooldowns[i].cooldown <= 0.0f)
                        {
                            chunk.SetComponentEnabled<AbilityCooldown>(ref this.cooldownTypeHandle, i, false);
                            chunk.SetComponentEnabled<AbilityReady>(ref this.readyTypeHandle, i, true);
                        }
                    }
                }
            }

            private ComponentTypeHandle<AbilityCooldown>    cooldownTypeHandle;
            private ComponentTypeHandle<AbilityReady>       readyTypeHandle;

            private EntityQuery                             updateCooldown;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<UpdateAbilityCooldown>(state.SystemHandle)));

                this.updateCooldown     = state.GetEntityQuery(new EntityQueryDesc
                {
                    All                 = new ComponentType[] { typeof(AbilityCooldown) }
                });

                this.cooldownTypeHandle = state.GetComponentTypeHandle<AbilityCooldown>(false);
                this.readyTypeHandle    = state.GetComponentTypeHandle<AbilityReady>(false);

            }

            public void OnUpdate(ref SystemState state)
            {
                this.cooldownTypeHandle.Update(ref state);
                this.readyTypeHandle.Update(ref state);

                var updateCooldown      = new UpdateCooldown
                {
                    cooldownTypeHandle  = this.cooldownTypeHandle,
                    readyTypeHandle     = this.readyTypeHandle,
                    deltaTime           = state.World.Time.DeltaTime
                };

                updateCooldown.ScheduleParallel(this.updateCooldown, state.Dependency).Complete();
            }
        }

        [CreateAfter(typeof(EventQueue))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenGameOver, false)]
        public partial class AbilitySystem : SystemBase, IEventListener<AbilitySystem>
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

            //private EntityQuery                     updateActive;

            private NativeHashMap<EntityAbilityBinding, Entity> learnedEntityAbilities;
            private NativeList<UseAbilityEvent<Entity>>         usedAbilities;


            private NativeHashMap<FixedString64Bytes, Entity>   name2Ability;

            

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

            protected override void OnCreate()
            {
                this.RequireForUpdate(StateManager.state(this));

                Addressables.LoadAssetsAsync<AbilityDescription>(AbilityDescription.label, this.instanciateAbility);

                this.RequireAnyForUpdate(new EntityQuery[]
                {
                    this.GetEntityQuery(typeof(Ability)),
                });


                this.name2Ability               = new NativeHashMap<FixedString64Bytes, Entity>(32, Allocator.Persistent);

                this.abilityDataLookup          = this.GetComponentLookup<AbilityData>(true);
                this.abilityMetaLookup          = this.GetComponentLookup<AbilityMeta>(true);
                this.abilityCoolLookup          = this.GetComponentLookup<AbilityCooldown>(false);
                this.abilityReadyLookup         = this.GetComponentLookup<AbilityReady>(false);

                

                //this.updateActive       = state.GetEntityQuery(new EntityQueryDesc
                //{
                //    All                 = new ComponentType[] { typeof(AbilityActive) },
                //});


                this.learnedEntityAbilities     = new NativeHashMap<EntityAbilityBinding, Entity>(128, Allocator.Persistent);
                this.usedAbilities              = new NativeList<UseAbilityEvent<Entity>>(128, Allocator.Persistent);

                EventQueue.subscribe(this);
            }

            private void instanciateAbility(AbilityDescription description)
            {
                Debug.Log($"instanciate ability {description.meta.name}");
                var abilityEntity = this.EntityManager.CreateEntity();
                {
#if UNITY_EDITOR
                    this.EntityManager.SetName(abilityEntity, $"{AbilityDescription.label}-{description.meta.name}");
#endif
                    this.EntityManager.AddComponent(abilityEntity, new ComponentTypeSet(new ComponentType[]
                    {
                                typeof(AbilityData),
                                typeof(AbilityMeta),
                    }));

                    this.EntityManager.SetComponentData<AbilityData>(abilityEntity, new AbilityData
                    {
                        abilityPrefabID = description.abilityPrefab.GetInstanceID()
                    });
                    this.EntityManager.SetComponentData<AbilityMeta>(abilityEntity, description.meta);
                }
                if(!this.name2Ability.TryAdd(description.meta.name, abilityEntity)) { this.name2Ability[description.meta.name] = abilityEntity; }
            }

            protected override void OnDestroy()
            {
                this.learnedEntityAbilities.Dispose();
                this.usedAbilities.Dispose();
                this.name2Ability.Dispose();
            }

            protected override void OnUpdate()
            {
                this.abilityDataLookup.Update(this);
                this.abilityMetaLookup.Update(this);
                this.abilityCoolLookup.Update(this);
                this.abilityReadyLookup.Update(this);

                // active triggered abilities
                if(!this.usedAbilities.IsEmpty)
                {
                    var buffer = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(this.World.Unmanaged);
                    foreach(var used in this.usedAbilities)
                    {
                        var binding             = new EntityAbilityBinding(used.entity, used.ability);

                        if(!this.learnedEntityAbilities.TryGetValue(binding, out Entity ability)) { continue; }

                        var data                = this.abilityDataLookup[used.ability];
                        var meta                = this.abilityMetaLookup[used.ability];

                        // stop right here, if ability is not ready yet
                        if(!this.abilityReadyLookup.IsComponentEnabled(ability))
                        {
                            //Debug.Log($"{used.entity}: ability '{meta.name}' is not ready yet. [{this.abilityCoolLookup.GetRefRO(ability).ValueRO.cooldown}]");
                            continue;
                        }

                        spawn.request.create((GameObject)Resources.InstanceIDToObject(data.abilityPrefabID), new Vector3(used.point.x, used.point.y, 0f), 0f, (GameObject abilityGO) =>
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

                        //Debug.Log($"{used.entity}: used ability '{meta.name}'.");
                    }
                    this.usedAbilities.Clear();
                }
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
                var entityAbility       = entityManager.CreateEntity(new ComponentType[]
                {
                    typeof(Ability),
                    typeof(AbilityReady),
                    //typeof(AbilityActive),
                    typeof(AbilityCooldown)
                });

#if UNITY_EDITOR
                var entityName  = entityManager.GetName(e.entity);
                entityName      = string.IsNullOrEmpty(entityName) ? e.entity.ToString() : entityName;

                var meta        = entityManager.GetComponentData<AbilityMeta>(e.ability);

                entityManager.SetName(entityAbility, $"{meta.name}-{entityName}");
                Debug.Log($"'{entityName}' learned '{meta.name}' ability.");
#endif

                entityManager.SetComponentData<Ability>(entityAbility, new Ability
                {
                    owner           = e.entity,
                    ability         = e.ability
                });
                entityManager.SetComponentEnabled<AbilityReady>(entityAbility, true);
                //entityManager.SetComponentEnabled<AbilityActive>(abilityEntity, false);
                entityManager.SetComponentEnabled<AbilityCooldown>(entityAbility, false);

                this.learnedEntityAbilities.Add(new EntityAbilityBinding(e.entity, e.ability), entityAbility);
            }

            void onEntityDiedEvent(EntityDiedEvent e)
            {
                foreach(var binding in this.learnedEntityAbilities.GetKeyArray(Allocator.Temp))
                {
                    if(binding.entity == e.entity)
                    {
                        this.EntityManager.DestroyEntity(this.learnedEntityAbilities[binding]);
                        this.learnedEntityAbilities.Remove(binding);
                    }
                }
            }

            void onGameOverEvent(GameOverEvent e)
            {
                foreach(var binding in this.learnedEntityAbilities.GetKeyArray(Allocator.Temp))
                {
                    this.EntityManager.DestroyEntity(this.learnedEntityAbilities[binding]);
                }

                this.learnedEntityAbilities.Clear();
            }
        }
    }
}
