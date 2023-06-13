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
            public override IEnumerator<StateBase> execute(Generator.Context context, StateBase lastStepState)
            {
                for(int i = 0; i < context.level.numChunks; i++)
                {
                    var chunk = context.level.getChunk(i);
                    var start = context.level.getChunk(0).bounds.size / 2;

                    if(i > 0)
                    {
                        var lastChunkInfo = context.level.getChunk(i - 1);
                        var offset = lastChunkInfo.bounds.position - chunk.bounds.position;
                        offset += new Vector2Int(offset.x != 0 ? -(int)Mathf.Sign(offset.x) : 0, offset.y != 0 ? -(int)Mathf.Sign(offset.y) : 0);

                        start = lastChunkInfo.pathExit + offset;
                    }

                    var tileId = (start.y * chunk.bounds.width) + start.x;
                    var tileData = chunk.data[tileId];

                    tileData.constructionType = LevelData.Tile.ConstructionType.Walkable;
                    chunk.data[tileId] = tileData;

                    var exit = chunk.pathExit;

                    var from        = i > 0                                 ? context.level.chunks[i - 1].bounds.position - context.level.chunks[i].bounds.position : Vector2Int.zero;
                    var to          = i < context.level.chunks.Length - 1   ? context.level.chunks[i + 1].bounds.position - context.level.chunks[i].bounds.position : Vector2Int.zero;

                    from            = new Vector2Int(Mathf.FloorToInt(from.x    / from.magnitude), Mathf.FloorToInt(from.y  / from.magnitude));
                    to              = new Vector2Int(Mathf.FloorToInt(to.x      / to.magnitude),   Mathf.FloorToInt(to.y    / to.magnitude));


                    // extends the entry from the wall intp the chunk (room)
                    for(int s = 0; s < chunk.wallSize + 1; s++)
                    {
                        tileId = (start.y * chunk.bounds.width) + start.x;
                        tileData = chunk.data[tileId];

                        tileData.constructionType = LevelData.Tile.ConstructionType.Walkable;
                        chunk.data[tileId] = tileData;

                        start -= from;
                    }

                    // extends the exit path from the wall into the chunk (room)
                    for(int s = 0; s < chunk.wallSize + 1; s++)
                    {
                        tileId = (exit.y * chunk.bounds.width) + exit.x;
                        tileData = chunk.data[tileId];

                        tileData.constructionType = LevelData.Tile.ConstructionType.Walkable;
                        chunk.data[tileId] = tileData;

                        exit -= to;
                    }

                    // do random walk inside the map chunk with a 1 tile margin to the borders
                    {
                        RectInt area = new RectInt
                        {
                            x = chunk.wallSize + 1,
                            y = chunk.wallSize + 1,
                            width = chunk.bounds.width - chunk.wallSize - 1,
                            height = chunk.bounds.height - chunk.wallSize - 1
                        };
                        NativeList<Vector2Int> options = new NativeList<Vector2Int>(4, Allocator.Persistent);

                        var currentPos = start;
                        tileId = (currentPos.y * chunk.bounds.width) + currentPos.x;
                        tileData = chunk.data[tileId];

                        tileData.constructionType = LevelData.Tile.ConstructionType.Walkable;
                        chunk.data[tileId] = tileData;

                        while(currentPos != exit)
                        {
                            this.walkOptions(ref options, area, currentPos);

                            currentPos = options[context.random.Next(0, options.Length)];

                            tileId = (currentPos.y * chunk.bounds.width) + currentPos.x;
                            tileData = chunk.data[tileId];

                            tileData.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunk.data[tileId] = tileData;
                        }

                        options.Dispose();
                    }
                }

                // since we changed the chunk tiles, we need to ensure all overlapping chunks
                // are getting merged, again.
                var mergeJob = new CreateMazeWallsStep.MergeOverlappingChunksJob
                {
                    chunks = context.level.chunks,
                };
                context.schedule(mergeJob);

                yield return StateBase.Default;
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
