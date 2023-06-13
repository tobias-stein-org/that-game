using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace tg.level.generator
{
    public class Generator
    {
        #region Generator context

        public class Context : IDisposable
        {
            public  readonly GeneratorSettings  settings;

            public  System.Random               random  { get; private set; }

            /// <summary>
            /// Unmanaged data (no garbage collection). Pipeline steps are allocating and freeing resources.
            /// </summary>
            private LevelData                   unmanaged;
                                                
            private Queue<JobHandle>            pendingJobs;
                                                
            public  ref LevelData               level { get { return ref this.unmanaged; } }


            public Context(GeneratorSettings settings)
            {
                this.settings = settings;
            }

            /// <summary>
            /// Called at runtime to initialize the runtime settings.
            /// </summary>
            public void initialize()
            {
                this.random         = this.settings.seed != 0 ? new System.Random(this.settings.seed) : new System.Random();
                this.pendingJobs    = new Queue<JobHandle>(64);
            }

            public bool hasPendingJobs
            {
                get
                {
                    while(this.pendingJobs.Count > 0 && this.pendingJobs.Peek().IsCompleted)
                    {
                        this.pendingJobs.Dequeue().Complete();
                    }
    
                    return this.pendingJobs.Count > 0;
                }
            }
    
            public void awaitPendingJobs()
            {
                while(this.pendingJobs.TryDequeue(out JobHandle jobHandle))
                {
                    jobHandle.Complete();
                }
            }

            public JobHandle schedule<T>(T job, JobHandle dependsOn = default)
                where T: struct, IJob
            {
                var handle = IJobExtensions.Schedule(job, dependsOn);
                this.pendingJobs.Enqueue(handle);

                return handle;
            }

            public JobHandle schedule<T>(T job, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
                where T: struct, IJobParallelFor
            {
                var handle = IJobParallelForExtensions.Schedule(job, arrayLength, innerloopBatchCount, dependsOn);
                this.pendingJobs.Enqueue(handle);

                return handle;
            }

            public JobHandle scheduleBatch<T>(T job, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
                where T: struct, IJobParallelForBatch
            {
                var handle = IJobParallelForBatchExtensions.ScheduleBatch(job, arrayLength, innerloopBatchCount, dependsOn);
                this.pendingJobs.Enqueue(handle);

                return handle;
            }

            public void Dispose()
            {
                this.unmanaged.Dispose();
            }
        }

        #endregion

        #region Generator events

        public delegate void LevelGenerator_Start();
        public delegate void LevelGenerator_Finish(in LevelData levelData);

        public event LevelGenerator_Start              onLevelGeneratorStarted;
        public event LevelGenerator_Finish             onLevelGeneratorFinished;

        #endregion

        [NonSerialized]
        public  readonly Context                        context;

        [NonSerialized]
        public  readonly Pipeline                       pipeline;

        private readonly int                            frameTimeBudget;

        public  LevelData                               data { get { return this.context.level; } }

        /// <summary>
        /// Busy flag indicates, if it is safe to access LevelData. If its true level data should not be accessed.
        /// </summary>
        public  bool                                    isBusy { get { return this.context.hasPendingJobs; } }


        private  Generator() {}

        internal Generator(GeneratorSettings settings)
        {
            this.pipeline           = settings.steps.Aggregate(Pipeline.create(), (builder, step) => builder.add(step)).build();
            this.context            = new Context(settings);

            this.frameTimeBudget    = settings.maxFrameProcessTimeMs;

            this.context.initialize();
        }

        public  IEnumerator<LevelGeneratorStep.StateBase> execute()
        {
            var pipelineExecutor = this.pipeline.execute(this.context);
            var generatorStarted = DateTime.Now;
            this.onLevelGeneratorStarted?.Invoke();

            // update current pipeline step
            var maxSubStepDuration  = TimeSpan.FromMilliseconds(this.frameTimeBudget);

            // note: its imperical that we check 'isBudy' first otherwise we might run into a "race condition" of previously scheduled async jobs
            while(this.isBusy || pipelineExecutor.MoveNext())
            {
                var t0                  = DateTime.Now;
                while(pipelineExecutor.Current != null && DateTime.Now - t0 < maxSubStepDuration)
                {

                    // do not attempt to update pipeline, if it is stil busy.
                    if(this.isBusy) { continue; }

                    // update generator pipeline
                    pipelineExecutor.MoveNext();
                }

                yield return pipelineExecutor.Current;
            }

            Debug.Log($"Generator finished in: {DateTime.Now - generatorStarted}");
            this.onLevelGeneratorFinished?.Invoke(this.context.level);
        }

        public void Dispose()
        {
            this.context.awaitPendingJobs();

            // allow each map gen step to release its previously initialized resources.
            // note: we start releasing the latest steps first as they might rely on resources from previous steps
            foreach(var step in this.pipeline.steps.Reverse())
            {
                step.release(this.context);
                Debug.Log($"Generator step {step.GetType()} released.");
            }

            Debug.Log("Dispose of generator context.");
            this.context.Dispose();
        }
    }
}