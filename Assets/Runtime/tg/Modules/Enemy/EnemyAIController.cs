using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace tg.enemy
{
    using tg.application;
    using tg.ai;

    readonly partial struct AIEnemy : IAspect
    {
        readonly DynamicBuffer<BehaviourContextData>    behaviourContext;
        readonly RefRO<Enemy>                           enemy;

        public BehaviourContext                         solvedContext
        {
            get { return this.behaviourContext[IBehaviourContext<Solved>.ID].context; }
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
            foreach(var (aiEnemy, rb) in SystemAPI.Query<AIEnemy, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>())
            {
                var context     = aiEnemy.solvedContext;
                var min         = context[0];
                var max         = context[0];
                var imin        = 0;
                var imax        = 0;

                for(int i = 0; i < BehaviourContext.segmentDir.Length; i++)
                {
                    var value   = context[i];

                    if(value > max) { max = value; imax = i; }
                    if(value < min) { min = value; imin = i; }
                }

                rb.Value.velocity = BehaviourContext.segmentDir[imax];
            }
        }
    }
}
