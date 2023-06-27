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
                    if(player.isValid && state.EntityManager.HasComponent<Pursue>(aiEnemy.entity))
                    {
                        var pursue          = state.EntityManager.GetComponentData<Pursue>(aiEnemy.entity);
                        {
                            pursue.target   = player.point;
                        }
                        state.EntityManager.SetComponentData<Pursue>(aiEnemy.entity,pursue);
                        state.EntityManager.SetComponentEnabled<Pursue>(aiEnemy.entity, true);
                        state.EntityManager.SetComponentEnabled<Wander>(aiEnemy.entity, false);
                    }
                    else
                    {
                        if(!state.EntityManager.IsComponentEnabled<Pursue>(aiEnemy.entity))
                        {
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
