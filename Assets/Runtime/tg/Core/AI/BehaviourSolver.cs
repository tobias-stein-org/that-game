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

    /// <summary>
    /// This system will ensure all AI behaviour context are reset to zero before any update.
    /// </summary>
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [UpdateInGroup(typeof(BehaviourSystemGroup), OrderFirst = true)]
    public partial struct BehaviourReset : ISystem
    {
        private EntityQuery                     aiEntitiesQuery;

        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<BehaviourReset>(state.SystemHandle));

            this.aiEntitiesQuery        = state.GetEntityQuery(typeof(BehaviourContextData));

            state.RequireForUpdate(this.aiEntitiesQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var query                   = new EntityQueryBuilder(Allocator.Temp).WithAllRW<BehaviourContextData>().Build(state.EntityManager);
            var chunks                  = query.ToArchetypeChunkArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk               = chunks[i];
                var bufferTypeHandle    = state.GetBufferTypeHandle<BehaviourContextData>();
                var buffers             = chunk.GetBufferAccessor(ref bufferTypeHandle);

                for(int j = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
                {
                    var buffer = buffers[i];
                    for (int k = 0; k < buffer.Length; k++)
                    {
                        var contextData = buffer[k];
                        {
                            contextData.context  = BehaviourContext.zero;
                        }
                        buffer[k] = contextData;
                    }
                }
            }

            chunks.Dispose();
        }
    }

    /// <summary>
    /// This system will run after all ai behaviour systems are have completed. It will produce the final solved (combined) context.
    /// </summary>
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [UpdateInGroup(typeof(BehaviourSystemGroup), OrderLast = true)]
    public partial struct BehaviourSolver : IBehaviour<Solved>
    {
        /// <summary>
        /// This struct will be used to reinterprete the BehaviourContextData struct. This will give us access to the private 'context1'
        /// which is used by the solver to store old frame context data.
        /// </summary>
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

                    for(int i = 0; i < buffer.Length; i++)
                    {
                        if(i == IBehaviourContext<Solved>.ID) { continue; }

                        ref var ctx             = ref buffer.ElementAt(i);

                        if(!ctx.context.isValid) { continue; }

                        float behaviourWeight   = math.max(0.0f, ctx.weight);
                        float behaviourBlend    = math.clamp(ctx.blend, 0.0f, 1.0f);

                        // compute final behaviour context state by blending current with last frames context
                        ctx.context1            = BehaviourContext.lerp(ctx.context, ctx.context1, behaviourBlend);

                        // accumulate final solved context states
                        result                  = result + (ctx.context1 * behaviourWeight);

                        sumWeights              += behaviourWeight;
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
