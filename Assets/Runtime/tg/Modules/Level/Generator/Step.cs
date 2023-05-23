using System;
using System.Collections.Generic;
using UnityEngine;

namespace tg.level.generator
{
    /// <summary>
    /// Helper class to expose the StepType property later used by the pipeline builder to instanciate
    /// corresponding steps from their settings class.
    /// </summary>
    public abstract class LevelGeneratorStepSettings : ScriptableObject
    {
        public virtual Type StepType { get { throw new Exception($"{this.GetType().FullName} must be derived from LevelGeneratorStepSettings<TStep> parent."); } }
    }

    /// <summary>
    /// Base class for all concrete generator steps settings classes. A derived settings class must provide the class of its dedicated pipeline step as
    /// template parameter to LevelGeneratorStepSettings.
    /// </summary>
    /// <typeparam name="TStep"></typeparam>
    public class LevelGeneratorStepSettings<TStep> : LevelGeneratorStepSettings
    {
        public override Type StepType { get { return typeof(TStep); } }
    }

    public abstract class LevelGeneratorStep
    {
        public class State
        {
            internal static State Default => new State { };
        }

        public abstract void                  configure(LevelGeneratorStepSettings settings);

        /// <summary>
        /// Called by the generator before execution of this step. This allows each step to allocate
        /// its required resources.
        /// </summary>
        /// <param name="context"></param>
        public abstract void                  initialize(Generator.Context context);

        /// <summary>
        /// Called by the generator class to give each step the opportunity to free previously allocated resources.
        /// </summary>
        public abstract void                  release(Generator.Context context);

        /// <summary>
        /// Called by the generator to obtain a enumerator object to perform this concrete step.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract IEnumerator<State>    execute(Generator.Context context);
    }

    /// <summary>
    /// Base class for any concrete generator step implementation.
    /// </summary>
    /// <typeparam name="TStepSettings"></typeparam>
    public abstract class LevelGeneratorStep<TStepSettings> : LevelGeneratorStep
        where TStepSettings : LevelGeneratorStepSettings
    {
        protected TStepSettings settings;

        public LevelGeneratorStep()
        {}

        /// <summary>
        /// Applies new settings to the current generator step instance.
        /// </summary>
        /// <param name="settings"></param>
        public override void configure(LevelGeneratorStepSettings settings)
        {
            this.settings = (TStepSettings)settings;
        }
    }
}