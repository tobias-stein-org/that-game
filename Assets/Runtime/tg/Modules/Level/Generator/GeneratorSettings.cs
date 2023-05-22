using System.Collections.Generic;
using UnityEngine;

namespace tg.level.generator
{
    /// <summary>
    /// Level generator settings class.
    /// </summary>
    [CreateAssetMenu(menuName = "tg/Level/Generator Settings")]
    public class GeneratorSettings : ScriptableObject
    {
        /// <summary>
        /// Globally used random seed for generation process.
        /// Note: If the seed value is zero (0) the generator will use a random seed each time
        /// the application is executed.
        /// </summary>
        public  int                                 seed                    = 0;

        /// <summary>
        /// Maximum frame time (milliseconds) budget the generator is allowed to execute generator steps.
        /// Note: higher values may result in decreased frame rates.
        /// </summary>
        [Range(0, 20)]
        public  int                                 maxFrameProcessTimeMs   = 5;

        /// <summary>
        /// Generator steps. Order matters!
        /// </summary>
        public  List<LevelGeneratorStepSettings>    steps;
    }
}
