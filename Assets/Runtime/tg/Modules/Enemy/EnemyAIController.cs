using System.Linq;

using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;

namespace tg.enemy
{
    using tg.application;
    using tg.ai;
    using tg.ai.behaviour;
    using tg.ability.events;
    using tg.events;

    readonly partial struct AIEnemy : IAspect
    {
        readonly DynamicBuffer<BehaviourContextData>        behaviourContext;
        readonly RefRO<Enemy>                               enemy;
        readonly RefRW<EnemyInputData>                      inputData;

        public  Entity                                      entity { get { return this.enemy.ValueRO.entity; } }

        public BehaviourSolver.BehaviourContextDataInternal solvedContext
        {
            get { return this.behaviourContext.Reinterpret<BehaviourSolver.BehaviourContextDataInternal>()[IBehaviourContext<Solved>.ID]; }
        }

        public EnemyInputData                               input
        {
            get { return this.inputData.ValueRO; }
            set { this.inputData.ValueRW = value; }
        }
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EnemyAIController : ISystem
    {
        private Unity.Mathematics.Random  random;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<EnemyAIController>(state.SystemHandle)));

            this.random = Unity.Mathematics.Random.CreateFromIndex((uint)state.SystemHandle.GetHashCode());
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (aiEnemy, sensor, rb) in SystemAPI.Query<AIEnemy, Sensor, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>())
            {
                // TODO: at some point this logic has to be moved to a behviour tree
                {
                    var player  = sensor.query().withTag(tags.Player).FirstOrDefault();

                    // if enemy can see a player AND has the Pursue behaviour ...
                    if(player.isValid && state.EntityManager.HasComponent<Pursue>(aiEnemy.entity))
                    {
                        var pos             = (float2)rb.Value.position;
                        var dir             = math.normalize(player.point - (float2)rb.Value.position);

                        if(player.distance > 1.5f)
                        {
                            if(UnityEngine.Random.value < 0.2f)
                            {
                                EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                                {
                                    entity      = aiEnemy.entity,
                                    ability     = "FIREBALL_1",
                                    point       = pos + (dir * 1.5f),
                                    direction   = dir
                                });
                            }
                        }
                        // we are in attack range ...
                        else
                        {
                            if(UnityEngine.Random.value < 0.1f)
                            {
                                EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                                {
                                    entity      = aiEnemy.entity,
                                    ability     = "MELEE_ATT",
                                    point       = pos + (dir * 1.5f),
                                    direction   = dir
                                });
                            }
                        }


                        // start pursue the current percied player location
                        var pursue          = state.EntityManager.GetComponentData<Pursue>(aiEnemy.entity);
                        {
                            pursue.target   = player.point;
                        }
                        state.EntityManager.SetComponentData<Pursue>(aiEnemy.entity,pursue);
                        state.EntityManager.SetComponentEnabled<Pursue>(aiEnemy.entity, true);

                        // stop any wandering
                        state.EntityManager.SetComponentEnabled<Wander>(aiEnemy.entity, false);
                    }
                    // if we can't see a player ...
                    else
                    {
                        // AND we are not Pursueing any one ...
                        if(!state.EntityManager.IsComponentEnabled<Pursue>(aiEnemy.entity))
                        {
                            // go back to wandering
                            state.EntityManager.SetComponentEnabled<Wander>(aiEnemy.entity, true);
                        }
                    }
                }

                var behaviour       = aiEnemy.solvedContext;
                var context         = behaviour.context.normalize();
                var csum            = context.csum();

                if(csum > 1e-5f)
                {
                    var rng         = this.random.NextFloat(csum);
                    var i           = 0;
                    var acc         = 0.0f;
                    for(i           = 0; i < context.length - 1; i++)
                    {
                        if(acc      >= rng) { break; }
                        acc         += context[i];
                    }

                    var input       = aiEnemy.input;

                    rb.Value.AddForce(BehaviourContext.segmentDir[i] - rb.Value.velocity, ForceMode2D.Force);
                    //rb.Value.velocity = Vector2.LerpUnclamped(BehaviourContext.segmentDir[i], BehaviourContext.segmentDir[input.lastBehaviourContextDecision], rb.Value.velocity.magnitude * behaviour.blend);

                    input.lastBehaviourContextDecision = (byte)i;

                    aiEnemy.input   = input;
                }
            }
        }
    }
}
