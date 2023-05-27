using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tg.level.generator.step
{
    [CreateAssetMenu(menuName = "tg/Level/Steps/Create Maze")]
    public class CreateMazeStepSettings : LevelGeneratorStepSettings<CreateMazeStep>
    {
        /// <summary>
        /// Origion of the first maze element
        /// </summary>
        public Vector2Int   pathStart               = Vector2Int.zero;

        /// <summary>
        /// Total length of generated path.
        /// </summary>
        public int          pathLength              = 7;

        /// <summary>
        /// Influences how straight a path will be.
        /// </summary>
        [Range(0, 1)]
        public float        pathStraightness        = 0.2f;

        /// <summary>
        /// Influences the overall curvature of the generated path.
        /// </summary>
        [Range(0, 1)]       
        public float        pathCurvature           = 0.7f;

        /// <summary>
        /// A measure of paht density. Given the bounding box of a path this measure describes the ratio between the path and bounding box area.
        /// More dense path have less gaps/space between path sections.
        /// </summary>
        [Range(0, 1)]       
        public float        pathDensity             = 0.1f;

        /// <summary>
        /// Influence of randomness when choosing the next path element.
        /// </summary>
        [Range(0, 1)]       
        public float        pathRandomness          = 0.0f;

        /// <summary>
        /// Allow a generated path element to intersect a previous genered path element.
        /// Note: Enabling this turn the algorithm basically into a random walk.
        /// </summary>
        public bool         pathIntersection        = false;

        /// <summary>
        /// If the algorithm runs into a deadend during the generation process allow it to backtrack and try previously
        /// generated path alternatives.
        /// </summary>
        public bool         allowBacktracking       = true;

        /// <summary>
        /// Maximum number of allowed backtracks before retrying from scratch.
        /// </summary>
        public int          maxBacktracks           = 30;

        public void OnEnable()
        {
            // ensure weights are normalized
            float sum = this.pathStraightness + this.pathCurvature + this.pathDensity;
            this.pathStraightness    /= sum;
            this.pathCurvature       /= sum;
            this.pathDensity         /= sum;
        }
    }
}
