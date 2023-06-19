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
    /// This system will ensure all AI behaviour context are invalidated before next update.
    /// </summary>
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    [UpdateInGroup(typeof(BehaviourSystemGroup), OrderFirst = true)]
    public partial struct BehaviourReset : ISystem
    {
        private EntityQuery                             aiEntitiesQuery;
        private BufferTypeHandle<BehaviourContextData>  bufferTypeHandle;

        public void OnCreate(ref SystemState state)
        {
            StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<BehaviourReset>(state.SystemHandle));

            this.aiEntitiesQuery        = state.GetEntityQuery(typeof(BehaviourContextData));

            state.RequireForUpdate(this.aiEntitiesQuery);
            this.bufferTypeHandle       = state.GetBufferTypeHandle<BehaviourContextData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            this.bufferTypeHandle.Update(ref state);

            var query                   = new EntityQueryBuilder(Allocator.Temp).WithAllRW<BehaviourContextData>().Build(state.EntityManager);
            var chunks                  = query.ToArchetypeChunkArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++)
            {
                var chunk               = chunks[i];
                var buffers             = chunk.GetBufferAccessor(ref this.bufferTypeHandle);

                for(int j = 0, chunkEntityCount = chunk.Count; j < chunkEntityCount; j++)
                {
                    var buffer = buffers[j];
                    for (int k = 0; k < buffer.Length; k++)
                    {
                        var contextData = buffer[k];
                        {
                            contextData.context  = default;
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

        private EntityQuery                 aiEntitiesQuery;

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
                        float solveWeight       = math.max(0.0f, solved.weight);
                        float solveBlend        = math.clamp(solved.blend, 0.0f, 1.0f);

                        // weighted avg.
                        if(sumWeights > 1e-5f)  { result = result / sumWeights; }

                        result                  = result.blur(size: 4, strength: solveWeight).normalize();
                        for(int i = 0; i < result.length; i++)
                        {
                            if(result[i] < 0.0f)
                            {
                                var a           = i - 1 < 0  ? 15 : i - 1;
                                var b           = i + 1 > 15 ? 0  : i + 1;

                                var distribute  = math.abs(result[i]) * 0.5f;

                                result[a]       = result[a] > 0.0f ? math.max(0.0f, result[a] - distribute) : result[a];
                                result[b]       = result[b] > 0.0f ? math.max(0.0f, result[b] - distribute) : result[b];
                                result[i]       = 0.0f;
                            }
                        }
                        result                  = result.blur(size: 2, strength: solveWeight * 0.5f);//.normalize();


                        solved.context          = BehaviourContext.lerp(result, solved.context1, solveBlend);
                        solved.context1         = solved.context;
                    }
                }
            }

        }
    }
}
