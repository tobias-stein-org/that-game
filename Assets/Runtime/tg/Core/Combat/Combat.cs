using Unity.Jobs;
using Unity.Burst;
using Unity.Burst.Intrinsics;

using Unity.Entities;
using Unity.Collections;

using FrameDamageBuffer = Unity.Collections.FixedList512Bytes<tg.combat.Damage>;

namespace tg.combat
{
    using tg.events;
    using tg.combat.events;

    namespace entities
    {
        using tg.application.entities;
        using static UnityEngine.EventSystems.EventTrigger;

        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        public partial class CombatSystemGroup : ComponentSystemGroup
        {
        }

        /// <summary>
        /// Simple system that will listen to any DealDamage events, buffer them and
        /// distribute the accumulated frame damage to the appropriate target entities.
        /// </summary>
        [CreateAfter(typeof(EventQueue))]
        [UpdateInGroup(typeof(CombatSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial class DamageListener : SystemBase, IEventListener<DamageListener>
        {
            private struct ProcessDamageFrameBuffer : IJobParallelFor
            {
                [ReadOnly]
                public NativeArray<Entity>                      targets;

                [ReadOnly]
                public NativeHashMap<Entity, FrameDamageBuffer> buffers;

                [ReadOnly]
                public ComponentLookup<Damagaeble>              isDamageable;

                public EntityCommandBuffer.ParallelWriter       ecb;

                public void Execute(int index)
                {
                    var target  = this.targets[index];
                    if(this.isDamageable.TryGetComponent(target, out Damagaeble data))
                    {
                        data.frameDamage = this.buffers[target];
                        this.ecb.SetComponent<Damagaeble>(index, target, data);
                    }
                }
            }

            private NativeHashMap<Entity, FrameDamageBuffer>    frameDamageBuffer;

            private ComponentLookup<Damagaeble>                 isDamageable;


            protected override void OnCreate()
            {
                this.RequireForUpdate(StateManager.state(this));

                this.frameDamageBuffer  = new NativeHashMap<Entity, FrameDamageBuffer>(64, Allocator.Persistent);
                this.isDamageable       = this.GetComponentLookup<Damagaeble>(true);
            }

            protected override void OnStartRunning()
            {
                EventQueue.subscribe(this);
            }

            protected override void OnStopRunning()
            {
                EventQueue.unsubscribe(this);
            }

            protected override void OnDestroy()
            {
                if(this.frameDamageBuffer.IsCreated) { this.frameDamageBuffer.Dispose(); }
            }

            protected override void OnUpdate()
            {
                this.isDamageable.Update(this);

                if(!this.frameDamageBuffer.IsEmpty)
                {
                    var targets         = this.frameDamageBuffer.GetKeyArray(Allocator.TempJob);
                    var ecb             = new EntityCommandBuffer(Allocator.TempJob);
                    var process         = new ProcessDamageFrameBuffer
                    {
                        targets         = targets,
                        buffers         = this.frameDamageBuffer,
                        isDamageable    = this.isDamageable,
                        ecb             = ecb.AsParallelWriter()
                    };

                    this.Dependency     = process.Schedule(targets.Length, targets.Length / 6, this.Dependency);

                    this.Dependency.Complete();
                    this.frameDamageBuffer.Clear();

                    ecb.Playback(this.EntityManager);
                }
            }

            void onDealDamageEvent(DealDamageEvent e)
            {
                if(!this.frameDamageBuffer.TryGetValue(e.target, out FrameDamageBuffer buffer))
                {
                    buffer = new FrameDamageBuffer();
                }

                if(buffer.Length < buffer.Capacity)
                {
                    buffer.Add(e.damage);
                    this.frameDamageBuffer[e.target] = buffer;
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Entity '{e.target}' frame-damage-buffer capacity '{buffer.Capacity}' reached.");
                }
            }
        }

        /// <summary>
        /// Resolves the accumulated frame damage per entity and calculates a single (resolved) damage value.
        /// </summary>
        [BurstCompile]
        [UpdateInGroup(typeof(CombatSystemGroup))]
        [UpdateAfter(typeof(DamageListener))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct DamageSolver : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<DamageSolver>(state.SystemHandle)));
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                foreach(var (data, entity) in SystemAPI.Query<RefRW<Damagaeble>>().WithEntityAccess())
                {
                    data.ValueRW.resolvedDamage = 0f;

                    float resolvedDamage = 0f;

                    for(int i = 0; i < data.ValueRO.frameDamage.Length; i++)
                    {
                        var damage = data.ValueRO.frameDamage[i];

                        UnityEngine.Debug.Log($"{damage.source} '{damage.name}' deals '{damage.value}' to {entity}");
                        resolvedDamage += damage.value;
                    }

                    data.ValueRW.resolvedDamage = resolvedDamage;
                    data.ValueRW.frameDamage.Clear();
                }
            }
        }

        [BurstCompile]
        [UpdateInGroup(typeof(CombatSystemGroup))]
        [UpdateAfter(typeof(DamageListener))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct UpdateHealth : ISystem
        {
            [BurstCompile]
            private struct UpdateHealthJob : IJobChunk
            {
                public ComponentTypeHandle<Health>      healthTypeHandle;

                [ReadOnly]
                public ComponentTypeHandle<Damagaeble>  damageTypeHandle;

                [BurstCompile]
                public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
                {
                    var health          = chunk.GetNativeArray(ref this.healthTypeHandle);
                    var damage          = chunk.GetNativeArray(ref this.damageTypeHandle);

                    var enumerator      = new ChunkEntityEnumerator(useEnabledMask, chunkEnabledMask, chunk.Count);

                    while(enumerator.NextEntityIndex(out var i))
                    {
                        if(damage[i].resolvedDamage > 0f)
                        {
                            var _health     = health[i];
                            _health.health -= damage[i].resolvedDamage;
                            health[i]       = _health;

                            if(_health.health <= 0f)
                            {
                                chunk.SetComponentEnabled<Health>(ref this.healthTypeHandle, i, false);
                            }
                        }
                    }
                }
            }

            private ComponentTypeHandle<Health>     healthTypeHandle;
            private ComponentTypeHandle<Damagaeble> damageTypeHandle;

            private EntityQuery                     updateHealth;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<DamageSolver>(state.SystemHandle)));

                this.updateHealth = state.GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { typeof(Health), typeof(Damagaeble) }
                });

                this.healthTypeHandle = state.GetComponentTypeHandle<Health>(false);
                this.damageTypeHandle = state.GetComponentTypeHandle<Damagaeble>(true);
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                this.healthTypeHandle.Update(ref state);
                this.damageTypeHandle.Update(ref state);

                var updateHealth = new UpdateHealthJob
                {
                    healthTypeHandle = this.healthTypeHandle,
                    damageTypeHandle = this.damageTypeHandle
                };

                updateHealth.ScheduleParallel(this.updateHealth, state.Dependency).Complete();
            }
        }


        [BurstCompile]
        [UpdateInGroup(typeof(CombatSystemGroup), OrderLast = true)]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct DiedEntities : ISystem
        {
            private EntityQuery diedEntities;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<DamageSolver>(state.SystemHandle)));

                this.diedEntities = state.EntityManager.CreateEntityQuery(new EntityQueryDesc { Disabled = new ComponentType[] { typeof(Health) } });
                this.diedEntities.SetChangedVersionFilter(typeof(Health));
                state.RequireForUpdate(this.diedEntities);
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                foreach(var entity in this.diedEntities.ToEntityArray(Allocator.Temp))
                {
                    EventQueue.publish(new EntityDiedEvent { entity = entity });
                }
            }
        }
    }
}
