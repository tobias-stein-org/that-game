using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tg.level.generator.step
{
    [CreateAssetMenu(menuName="tg/Level/Steps/Render Maze")]
    public class RenderMazeStepSettings : LevelGeneratorStepSettings<RenderMazeStep>
    {
        /// <summary>
        /// The similarity threshold determines, if one module can be placed next to another with respect of the visual similarity
        /// on their connecting borders. To determine the similarity the algorithm will use the CIEDE2000 color comparision. Using
        /// this method yields a floating point number comaring two RGB colors. A value bellow 1.0 means the color difference is
        /// hardly or not noticable at all for the human eye. The higher the value the more distiguished the colors are. As en example
        /// comparing red (1, 0, 0) and blue (0, 0, 1) yields a value of roughly 20. This threshold tells the algorithm not to place
        /// any modules next to each other that have a higher difference then this.
        /// </summary>
        [Range(0.0f, 100.0f)]
        public float            moduleSimilarityThreshold   = 2.0f;

        /// <summary>
        /// The algorithm is comaring all pixels color differences between two shared module borders. This threshold tells the algorithm
        /// how many pixels should be bellow the similarity threshold. This allows modules to have some color differences in their shared
        /// borders.
        /// </summary>
        [Range(0.0f, 99.99f)]
        public float            moduleSimilarityPercentile  = 95.0f;

        [Range(1e-5f, 1.0f)]
        public float            chunkObstructivness         = 0.05f;

        /// <summary>
        /// The maximum number of attempts for the algorithm to solve (render) a chunk before giving up and move on to the next.
        /// A value of zero means the algorithm will try forever, if the initial state of a chunk is solvable.
        /// </summary>
        public int              numAttemptsToSolveChunk     = 3;

        /// <summary>
        /// When the algorithm runs into a deadend it usually would discard all progress and restart solving a chunk (this would count as
        /// a failed attempt). Allowing the algorithm to backtrack to a sub-solution of a previous solved step can boost its runtime
        /// in solving the problem.
        /// Note: A higher number of allowed backtracks will increase the algorithms memory footprint and might result in out-of-memory
        /// errors.
        /// </summary>
        [Range(1, 100)]
        public int              numBacktrackingSteps        = 4;

        public void OnValidate()
        {
            this.numAttemptsToSolveChunk    = Mathf.Max(0, this.numAttemptsToSolveChunk);
            this.chunkObstructivness        = Mathf.Clamp(this.chunkObstructivness, 1e-5f, 1.0f);
            this.numBacktrackingSteps       = Mathf.Clamp(this.numBacktrackingSteps, 1, 100);
        }
    }
}
