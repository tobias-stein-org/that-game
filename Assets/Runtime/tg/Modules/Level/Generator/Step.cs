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
        internal virtual Type StepType { get { throw new Exception($"{this.GetType().FullName} must be derived from LevelGeneratorStepSettings<TStep> parent."); } }
    }

    /// <summary>
    /// Base class for all concrete generator steps settings classes. A derived settings class must provide the class of its dedicated pipeline step as
    /// template parameter to LevelGeneratorStepSettings.
    /// </summary>
    /// <typeparam name="TStep"></typeparam>
    public class LevelGeneratorStepSettings<TStep> : LevelGeneratorStepSettings
        where TStep : LevelGeneratorStep
    {
        internal override Type StepType { get { return typeof(TStep); } }
    }


    public abstract class LevelGeneratorStep
    {
        public class StateBase
        {
            internal static StateBase Default => new StateBase { };
        }

        internal virtual Type InputType     { get { throw new Exception($"{this.GetType().FullName} must be derived from LevelGeneratorStep<TStepSettings[, TIn, TOut]> parent."); } }
        internal virtual Type OutputType    { get { throw new Exception($"{this.GetType().FullName} must be derived from LevelGeneratorStep<TStepSettings[, TIn, TOut]> parent."); } }

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
        internal abstract IEnumerator<StateBase>  executeInternal(Generator.Context context, StateBase lastStepState);
    }

    /// <summary>
    /// Base class for any concrete generator step implementation.
    /// </summary>
    /// <typeparam name="TStepSettings"></typeparam>
    public abstract class LevelGeneratorStep<TStepSettings, TStateIn, TStateOut> : LevelGeneratorStep
        where TStepSettings : LevelGeneratorStepSettings
        where TStateIn      : LevelGeneratorStep.StateBase
        where TStateOut     : LevelGeneratorStep.StateBase
    {
        protected TStepSettings settings;

        internal override Type InputType        { get { return typeof(TStateIn); } }
        internal override Type OutputType       { get { return typeof(TStateOut); } }

        public LevelGeneratorStep()
        {}

        /// <summary>
        /// Applies new settings to the current generator step instance.
        /// </summary>
        /// <param name="settings"></param>
        public override void                            configure(LevelGeneratorStepSettings settings) { this.settings = (TStepSettings)settings; }

        internal sealed override IEnumerator<StateBase>     executeInternal(Generator.Context context, StateBase lastStepState) { return this.execute(context, (TStateIn)lastStepState); }
        public abstract IEnumerator<TStateOut>          execute(Generator.Context context, TStateIn lastStepState);
    }

    public abstract class LevelGeneratorStep<TStepSettings, TStateIn> : LevelGeneratorStep<TStepSettings, TStateIn, LevelGeneratorStep.StateBase>
        where TStepSettings : LevelGeneratorStepSettings
        where TStateIn      : LevelGeneratorStep.StateBase
    {}

    public abstract class LevelGeneratorStep<TStepSettings> : LevelGeneratorStep<TStepSettings, LevelGeneratorStep.StateBase, LevelGeneratorStep.StateBase>
        where TStepSettings : LevelGeneratorStepSettings
    {}
}