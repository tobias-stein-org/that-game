using UnityEngine;
using Unity.Entities;
using Unity.Collections;

namespace tg.enemy
{
    using tg.application;
    using tg.ai;

    readonly partial struct AIEnemy : IAspect
    {
        public readonly Entity                      entity;

        readonly DynamicBuffer<BehaviourContext>    behaviourContext;

        readonly RefRO<Enemy>                       enemy;

        public FixedList128Bytes<float> solvedContext
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
                //Debug.Log($"{aiEnemy.solvedContext[0]}, {aiEnemy.solvedContext[1]}");
            }
        }
    }
}
