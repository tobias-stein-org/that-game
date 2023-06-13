using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

namespace tg.level.generator
{
    /// <summary>
    /// A pipeline is a auxilary class used by the generator to process generator steps in sequence.
    /// </summary>
    public class Pipeline
    {
        public delegate void StepStart(in LevelGeneratorStep step, in LevelData levelData);
        public delegate void StepUpdate(in LevelGeneratorStep step, in LevelData levelData);
        public delegate void StepFinish(in LevelGeneratorStep step, in LevelData levelData);

        public event StepStart                      onStepStarted;
        public event StepUpdate                     onStepUpdated;
        public event StepFinish                     onStepFinished;

        /// <summary>
        /// Ordered list of pipeline steps.
        /// </summary>
        //public List<LevelGeneratorStep>             _steps;
        public IReadOnlyList<LevelGeneratorStep>    steps { get; private set; }

        public Pipeline()
        {
            this.steps = null;
        }

        /// <summary>
        /// Create a new instance of a pipeline builder.
        /// </summary>
        /// <returns></returns>
        public static Builder create() { return new Builder(); }

        #region Step enumerator

        public IEnumerator<LevelGeneratorStep.StateBase> execute(Generator.Context context)
        {
            var lastStepState       = LevelGeneratorStep.StateBase.Default;
            var steps               = this.steps.GetEnumerator();

            while(steps.MoveNext())
            {
                var step            = steps.Current;
                var stepStarted     = DateTime.Now;

                step.initialize(context);
                this.onStepStarted?.Invoke(step, context.level);

                var stepExecutor    = step.executeInternal(context, lastStepState);
                while(stepExecutor.MoveNext())
                {
                    lastStepState   = stepExecutor.Current;
                    yield return lastStepState;

                    this.onStepUpdated?.Invoke(step, context.level);
                }

                Debug.Log($"Generator step {step} finished in: {DateTime.Now - stepStarted}");
                this.onStepFinished?.Invoke(step, context.level);
            }
        }
       
        #endregion

        #region Builder

        public class Builder
        {
            internal readonly List<LevelGeneratorStep> steps = new List<LevelGeneratorStep>();

            /// <summary>
            /// Add new pipeline step. Create a default instance of the step and apply provided settings on it.
            /// </summary>
            /// <typeparam name="TStep"></typeparam>
            /// <typeparam name="TStepSettings"></typeparam>
            /// <param name="settings"></param>
            /// <returns></returns>
            public Builder add<TStep, TStepSettings>(in TStepSettings settings = null)
                where TStep         : LevelGeneratorStep<TStepSettings>, new()
                where TStepSettings : LevelGeneratorStepSettings
            {
                return this.add(new TStep(), settings);
            }

            /// <summary>
            /// Add new pipeline step instance. If any settings have been provided they will be applied as well.
            /// </summary>
            /// <typeparam name="TStep"></typeparam>
            /// <typeparam name="TStepSettings"></typeparam>
            /// <param name="step"></param>
            /// <param name="settings"></param>
            /// <returns></returns>
            public Builder add<TStep, TStepSettings>(TStep step, TStepSettings settings = null)
                where TStep         : LevelGeneratorStep<TStepSettings>, new()
                where TStepSettings : LevelGeneratorStepSettings
            {

                var lastStepOutput = this.steps.Count > 0 ? this.steps.Last().OutputType : typeof(LevelGeneratorStep.StateBase);
                Unity.Assertions.Assert.IsTrue(lastStepOutput == step.InputType, $"Step required input missmatch! '{step.GetType().Name}' requires '{step.InputType.Name}', but got '{lastStepOutput.Name}'");

                if(settings)
                {
                    step.configure(settings);
                }
                this.steps.Add(step);

                return this;
            }

            /// <summary>
            /// Add new step to pipeline by deriving the concrete step from the provided settings.
            /// </summary>
            /// <typeparam name="TStepSettings"></typeparam>
            /// <param name="settings"></param>
            /// <returns></returns>
            public Builder add<TStepSettings>(TStepSettings settings)
                where TStepSettings : LevelGeneratorStepSettings
            {
                var step = (LevelGeneratorStep)Activator.CreateInstance(settings.StepType);


                var lastStepOutput = this.steps.Count > 0 ? this.steps.Last().OutputType : typeof(LevelGeneratorStep.StateBase);
                Unity.Assertions.Assert.IsTrue(lastStepOutput == step.InputType, $"Step required input missmatch! '{step.GetType().Name}' requires '{step.InputType.FullName}', but got '{lastStepOutput.FullName}'");

                step.configure(settings);
                this.steps.Add(step);

                return this;
            }

            /// <summary>
            /// Finalize pipeline setup.
            /// </summary>
            /// <returns></returns>
            public Pipeline build() { return new Pipeline { steps = this.steps }; }
        }

        #endregion
    }
}