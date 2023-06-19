using System.Linq;

using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace tg.enemy
{
    using tg.application;
    using tg.ai;
    using tg.ai.behaviour;

    readonly partial struct AIEnemy : IAspect
    {
        readonly DynamicBuffer<BehaviourContextData>        behaviourContext;
        readonly RefRO<Enemy>                               enemy;

        public  Entity                                      entity { get { return this.enemy.ValueRO.entity; } }

        public BehaviourSolver.BehaviourContextDataInternal solvedContext
        {
            get { return this.behaviourContext.Reinterpret<BehaviourSolver.BehaviourContextDataInternal>()[IBehaviourContext<Solved>.ID]; }
        }
    }

    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    [UpdateAfter(typeof(BehaviourSolver))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [RequireMatchingQueriesForUpdate]
    public partial struct EnemyAIController : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<EnemyAIController>(state.SystemHandle));
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

                var contextData = aiEnemy.solvedContext;
                var context0    = contextData.context;
                var context1    = contextData.context1;
                var max0        = context0[0];
                var max1        = context1[0];
                var imax0       = 0;
                var imax1       = 0;

                for(int i = 0; i < BehaviourContext.segmentDir.Length; i++)
                {
                    var value0   = context0[i];
                    var value1   = context1[i];

                    if(value0 > max0) { max0 = value0; imax0 = i; }
                    if(value1 > max1) { max1 = value0; imax1 = i; }
                }

                var dir0 = BehaviourContext.segmentDir[imax0];
                var dir1 = BehaviourContext.segmentDir[imax1];

                rb.Value.velocity = Vector2.Lerp(dir0, dir1, contextData.blend);
            }
        }
    }
}
