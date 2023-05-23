using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace tg.level
{
    namespace generator.step
    {
        /// <summary>
        /// This generator step performs a random walk in each chunk. It starts walking at the chunk entry until
        /// it reaches the chunk exit.
        /// </summary>
        public class RandomWalkMazeStep : LevelGeneratorStep<RandomWalkMazeStepSettings>
        {
            public override IEnumerator<State> execute(Generator.Context context)
            {
                for(int i = 0; i < context.data.numChunks; i++)
                {
                    var chunkInfo                       = context.data.getChunkInfo(i);
                    var chunkData                       = context.data.getChunkData(i);
                    var start                           = context.data.getChunkInfo(0).bounds.size / 2;

                    if(i > 0)
                    {
                        var lastChunkInfo               = context.data.getChunkInfo(i - 1);
                        var offset                      = lastChunkInfo.bounds.position - chunkInfo.bounds.position;
                        offset                          += new Vector2Int(offset.x != 0 ? -(int)Mathf.Sign(offset.x) : 0, offset.y != 0 ? -(int)Mathf.Sign(offset.y) : 0);

                        start                           = lastChunkInfo.pathExit + offset;
                    }

                    var tileId                          = (start.y * chunkInfo.bounds.width) + start.x;
                    var tileData                        = chunkData[tileId];

                    tileData.constructionType           = LevelData.Tile.ConstructionType.Walkable;
                    chunkData[tileId]                   = tileData;

                    var exit                            = chunkInfo.pathExit;

                    var camefrom                        = i > 0                                 ? -context.data.pathSteps[i - 1] : Vector2Int.zero;
                    var goTo                            = i < context.data.pathSteps.Length     ?  context.data.pathSteps[i]     : Vector2Int.zero;


                    // extends the entry from the wall intp the chunk (room)
                    for(int s = 0; s < chunkInfo.wallSize + 1; s++)
                    {
                        tileId                          = (start.y * chunkInfo.bounds.width) + start.x;
                        tileData                        = chunkData[tileId];

                        tileData.constructionType       = LevelData.Tile.ConstructionType.Walkable;
                        chunkData[tileId]               = tileData;

                        start                           -= camefrom;
                    }

                    // extends the exit path from the wall into the chunk (room)
                    for(int s = 0; s < chunkInfo.wallSize + 1; s++)
                    {
                        tileId                          = (exit.y * chunkInfo.bounds.width) + exit.x;
                        tileData                        = chunkData[tileId];

                        tileData.constructionType       = LevelData.Tile.ConstructionType.Walkable;
                        chunkData[tileId]               = tileData;

                        exit                            -= goTo;
                    }

                    // do random walk inside the map chunk with a 1 tile margin to the borders
                    { 
                        RectInt                 area    = new RectInt
                        {
                            x                           = chunkInfo.wallSize        + 1,
                            y                           = chunkInfo.wallSize        + 1,
                            width                       = chunkInfo.bounds.width    - chunkInfo.wallSize - 1,
                            height                      = chunkInfo.bounds.height   - chunkInfo.wallSize - 1
                        };
                        NativeList<Vector2Int>  options = new NativeList<Vector2Int>(4, Allocator.Persistent);

                        var currentPos                  = start;
                        tileId                          = (currentPos.y * chunkInfo.bounds.width) + currentPos.x;
                        tileData                        = chunkData[tileId];

                        tileData.constructionType       = LevelData.Tile.ConstructionType.Walkable;
                        chunkData[tileId]               = tileData;

                        while(currentPos != exit)
                        {
                            this.walkOptions(ref options, area, currentPos);

                            currentPos                  = options[context.random.Next(0, options.Length)];

                            tileId                      = (currentPos.y * chunkInfo.bounds.width) + currentPos.x;
                            tileData                    = chunkData[tileId];

                            tileData.constructionType   = LevelData.Tile.ConstructionType.Walkable;
                            chunkData[tileId]           = tileData;
                        }

                        options.Dispose();
                    }
                }

                // since we changed the chunk tiles, we need to ensure all overlapping chunks
                // are getting merged, again.
                var mergeJob                = new CreateMazeWallsStep.MergeOverlappingChunksJob
                {
                    chunkInfo               = context.data.chunkInfo,
                    chunkData               = context.data.chunkData,
                };
                context.schedule(mergeJob);

                yield return State.Default;
            }

            private void walkOptions(ref NativeList<Vector2Int> options, in RectInt area, in Vector2Int current)
            {
                options.Clear();

                // walk left
                var walkLeft = current + Vector2Int.left;
                if(walkLeft.x >= area.x)
                    options.AddNoResize(walkLeft);

                // walk right
                var walkRight = current + Vector2Int.right;
                if(walkRight.x < area.width)
                    options.AddNoResize(walkRight);

                // walk top
                var walkTop = current + Vector2Int.up;
                if(walkTop.y < area.height)
                    options.AddNoResize(walkTop);

                // walk bottom
                var walkBottom = current + Vector2Int.down;
                if(walkBottom.y >= area.y)
                    options.AddNoResize(walkBottom);
            }

            public override void initialize(Generator.Context context)
            {
            }

            public override void release(Generator.Context context)
            {
            }
        }
    }
}
