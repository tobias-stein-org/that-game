using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime.Misc;
using UnityEngine;

namespace tg.level.generator.step
{
    [CreateAssetMenu(menuName="tg/Level/Steps/Create Maze Walls")]
    public class CreateMazeWallsStepSettings : LevelGeneratorStepSettings<CreateMazeWallsStep>
    {
        /// <summary>
        /// Width and height for each map chunk.
        /// </summary>
        public Vector2Int   mapChunkDimensions          = new Vector2Int(10, 10);

        /// <summary>
        /// Wall size surrounding each maze chnk. Can be zero.
        /// </summary>
        public int          mapChunkWallSize            = 1;

        /// <summary>
        /// Defines the width of the passage/exit to the next maze chunks. 
        /// </summary>
        public int          walkablePathThickness       = 1;

        /// <summary>
        /// Displacement of the maze chunks passage/exit from its center.
        /// Note: a value of zero means the exit will always be in the center of the wall.
        /// </summary>
        [Range(0, 1)]
        public float        walkablePathDisplacement    = 0.0f;


        public void OnValidate()
        {
            this.mapChunkDimensions.x   = Mathf.Max(5, this.mapChunkDimensions.x);
            this.mapChunkDimensions.y   = Mathf.Max(5, this.mapChunkDimensions.y);

            var minSideSize             = Mathf.Min(this.mapChunkDimensions.x, this.mapChunkDimensions.y);
            var maxWallSize             = Mathf.FloorToInt((float)minSideSize / 2.0f);
            this.mapChunkWallSize       = Mathf.Clamp(this.mapChunkWallSize, 0, maxWallSize);

            this.walkablePathThickness  = Mathf.Clamp(walkablePathThickness, 0, minSideSize - (2 * this.mapChunkWallSize));
        }
    }
}
