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
            }
        }
    }
}
