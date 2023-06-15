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
        private EntityQuery                     aiEntitiesQuery;

        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<BehaviourSolver>(state.SystemHandle));

            this.aiEntitiesQuery        = state.GetEntityQuery(typeof(BehaviourContext));

            state.RequireForUpdate(this.aiEntitiesQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            using(var entities = this.aiEntitiesQuery.ToEntityArray(Allocator.Temp))
            {
                foreach(var entity in entities)
                {
                    var buffer = state.EntityManager.GetBuffer<BehaviourContext>(entity, false);

                    var result                  = BehaviourContext.Empty;
                    var sumWeights              = 0.0f;
                    var activeBehaviour         = 0;

                    for(int i = 0; i < buffer.Length; i++)
                    {
                        if(i == IBehaviourContext<Solved>.ID) { continue; }

                        ref var ctx             = ref buffer.ElementAt(i);

                        if(!ctx.context.isValid())
                        {
                            ctx.context         = BehaviourContext.Invalid;
                            ctx.context1        = BehaviourContext.Invalid;
                            continue;
                        }


                        float behaviourWeight   = math.max(0.0f, ctx.weight);
                        float behaviourBlend    = math.clamp(ctx.blend, 0.0f, 1.0f);

                        if(!ctx.context1.isValid())
                        {
                            ctx.context1        = BehaviourContext.Empty;
                        }

                        for(int j = 0; j < result.Length; j++)
                        {
                            ctx.context1[j]     = math.lerp(ctx.context[j], ctx.context1[j], behaviourBlend);
                            result[j]           = result[j] + (ctx.context1[j] * behaviourWeight);
                        }

                        sumWeights              += behaviourWeight;
                        activeBehaviour++;

                        // invalidate this frames context
                        ctx.context             = BehaviourContext.Invalid;
                    }
                        UnityEngine.Debug.Log($"{result[0]}, {result[1]}");

                    ref var solved              = ref buffer.ElementAt(IBehaviourContext<Solved>.ID);
                    {
                        float solveBlend        = math.clamp(solved.blend, 0.0f, 1.0f);

                        // weighted avg.
                        //if(sumWeights > 1e-5f)  { result.mul(1.0f / sumWeights); }

                        // store last solved context
                        solved.context1         = solved.context.isValid() ? solved.context : BehaviourContext.Empty;

                        for(int i = 0; i < result.Length; i++)
                        {
                            result[i]           = math.lerp(solved.context1[i], result[i], solveBlend);
                        }


                        solved.context          = result;
                    }
                }
            }

        }
    }
}
