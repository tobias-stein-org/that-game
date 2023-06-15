using Unity.Entities;
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

namespace tg.ai
{
    using System;
    using tg.ai.behaviour;
    using tg.application;
    using tg.enemy;

    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct BehaviourSolver : IBehaviour<Solved>
    {
        public struct BehaviourContextDataInternal
        {
            /// <summary>
            /// Behaviour context values.
            /// </summary>
            public BehaviourContext         context;

            /// <summary>
            /// Last frames behaviours context.
            /// </summary>
            public BehaviourContext         context1;

            public float                    weight;
            public float                    blend;
        }

        private EntityQuery                     aiEntitiesQuery;

        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<BehaviourSolver>(state.SystemHandle));

            this.aiEntitiesQuery        = state.GetEntityQuery(typeof(BehaviourContextData));

            state.RequireForUpdate(this.aiEntitiesQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            using(var entities = this.aiEntitiesQuery.ToEntityArray(Allocator.Temp))
            {
                foreach(var entity in entities)
                {
                    var buffer = state.EntityManager.GetBuffer<BehaviourContextData>(entity, false).Reinterpret<BehaviourContextDataInternal>();

                    var result                  = BehaviourContext.zero;
                    var sumWeights              = 0.0f;
                    var activeBehaviour         = 0;

                    for(int i = 0; i < buffer.Length; i++)
                    {
                        if(i == IBehaviourContext<Solved>.ID) { continue; }

                        ref var ctx             = ref buffer.ElementAt(i);

                        if(!ctx.context.isValid) { continue; }

                        float behaviourWeight   = math.max(0.0f, ctx.weight);
                        float behaviourBlend    = math.clamp(ctx.blend, 0.0f, 1.0f);

                        ctx.context1            = BehaviourContext.lerp(ctx.context, ctx.context1, behaviourBlend);
                        result                  = result + (ctx.context1 * behaviourWeight);

                        sumWeights              += behaviourWeight;
                        activeBehaviour++;

                        // invalidate this frames context
                        ctx.context             = BehaviourContext.zero;
                    }

                    ref var solved              = ref buffer.ElementAt(IBehaviourContext<Solved>.ID);
                    {
                        float solveBlend        = math.clamp(solved.blend, 0.0f, 1.0f);

                        // weighted avg.
                        if(sumWeights > 1e-5f)  { result = result / sumWeights; }

                        solved.context          = BehaviourContext.lerp(result, solved.context1, solveBlend);
                        solved.context1         = solved.context;
                    }
                }
            }

        }
    }
}
