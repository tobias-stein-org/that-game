using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace tg.level
{
    public partial struct LevelData
    {
        
    }

    namespace generator.step
    {
        public class CreateMazeWallsStep : LevelGeneratorStep<CreateMazeWallsStepSettings>
        {
            /// <summary>
            /// Each parallel job will create walls surrounding the currently processed level chunk.
            /// No walls will be created on those side two chunks connect.
            /// </summary>
            private struct CreateWallsJob : IJobParallelFor
            {
                public int                                  mapChunkWallSize;
                public int                                  mapPathThickness;

                [ReadOnly]
                public NativeArray<Vector2Int>              pathSteps;

                [ReadOnly]
                public NativeArray<float>                   pathDisplacements;

                [NativeDisableParallelForRestriction] 
                public UnsafeList<LevelData.Chunk>          chunks;

                public void Execute(int chunkId)
                {
                    var from        = chunkId > 0                       ? -this.pathSteps[chunkId - 1]  : Vector2Int.zero;
                    var to          = chunkId < this.pathSteps.Length   ?  this.pathSteps[chunkId]      : Vector2Int.zero;

                    ref var chunk   = ref this.chunks.ElementAt(chunkId);

                    // draw walls
                    chunk.wallSize = this.mapChunkWallSize;
                    for(int y = 0; y < chunk.bounds.height; y++)
                    for(int x = 0; x < chunk.bounds.width;  x++)
                    {
                        var tileId  = (y * chunk.bounds.width) + x;
                        var tile    = chunk.data[tileId];

                        // top-left
                        if(x < this.mapChunkWallSize && y < this.mapChunkWallSize)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }
                        // top-right
                        else if(x > chunk.bounds.width - this.mapChunkWallSize - 1 && y < this.mapChunkWallSize)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }
                        // bottom-left
                        else if(x < this.mapChunkWallSize && y > chunk.bounds.height - this.mapChunkWallSize - 1)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }
                        // bottom-right
                        else if(x > chunk.bounds.width - this.mapChunkWallSize - 1 && y > chunk.bounds.height - this.mapChunkWallSize - 1)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }
                        // left wall
                        else if(x < this.mapChunkWallSize && from != Vector2Int.left && to != Vector2Int.left)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }

                        // right wall
                        else if(x >= (chunk.bounds.width - this.mapChunkWallSize) && from != Vector2Int.right && to != Vector2Int.right)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }

                        // top wall
                        else if(y >= (chunk.bounds.height - this.mapChunkWallSize) && from != Vector2Int.up && to != Vector2Int.up)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }

                        // bottom wall
                        else if(y < this.mapChunkWallSize && from != Vector2Int.down && to != Vector2Int.down)
                        {
                            tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                        }

                        chunk.data[tileId] = tile;
                    }

                    // draw path
                    chunk.pathExit = chunk.bounds.size / 2;
                    var displacement = this.pathDisplacements[chunkId];
                    if(to == Vector2Int.left)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunk.bounds.height - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                        {
                            var tileId = y * chunk.bounds.width;
                            var tile = chunk.data[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunk.data[tileId] = tile;
                        }

                        chunk.pathExit = new Vector2Int(0, pathStart + this.mapPathThickness / 2);
                    }
                    else if(to == Vector2Int.right)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunk.bounds.height - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                        {
                            var tileId = (y * chunk.bounds.width) + (chunk.bounds.width - 1);
                            var tile = chunk.data[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunk.data[tileId] = tile;
                        }

                        chunk.pathExit = new Vector2Int(chunk.bounds.width - 1, pathStart + this.mapPathThickness / 2);
                    }
                    else if(to == Vector2Int.up)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunk.bounds.width - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                        {
                            var tileId = ((chunk.bounds.height - 1) * chunk.bounds.width) + x;
                            var tile = chunk.data[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunk.data[tileId] = tile;
                        }

                        chunk.pathExit = new Vector2Int(pathStart + this.mapPathThickness / 2, chunk.bounds.height - 1);
                    }
                    else if(to == Vector2Int.down)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunk.bounds.width - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                        {
                            var tileId = x;
                            var tile = chunk.data[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunk.data[tileId] = tile;
                        }

                        chunk.pathExit = new Vector2Int(pathStart + this.mapPathThickness / 2, 0);
                    }
                }
            }

            /// <summary>
            /// This job ensures that overlapping chunks are gettings merged. This means wall openings
            /// are getting replicated.
            /// </summary>
            internal struct MergeOverlappingChunksJob : IJob
            {
                public UnsafeList<LevelData.Chunk>  chunks;

                public void Execute()
                {
                    for(var chunkAId = 0; chunkAId < this.chunks.Length; chunkAId++)
                    {
                        for(int chunkBId = chunkAId; chunkBId < this.chunks.Length; chunkBId++)
                        {
                            if(chunkAId == chunkBId) { continue; }

                            var chunkA = this.chunks[chunkAId];
                            var chunkB = this.chunks[chunkBId];

                            // two different chunks overlap
                            if(chunkA.bounds.position == chunkB.bounds.position)
                            {

                                // merge all walkable tiles from chunkB with chunkA
                                for(int y = 0; y < chunkA.bounds.height; y++)
                                for(int x = 0; x < chunkA.bounds.height; x++)
                                {
                                    var tileId = (y * chunkA.bounds.width) + x;
                                    var tile = chunkA.data[tileId];

                                    if(chunkB.data[tileId].constructionType == LevelData.Tile.ConstructionType.Walkable)
                                    {
                                        tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                                    }

                                    if(chunkB.data[tileId].constructionType == LevelData.Tile.ConstructionType.Undefined && chunkA.data[tileId].constructionType == LevelData.Tile.ConstructionType.Obstructed)
                                    {
                                        tile.constructionType = LevelData.Tile.ConstructionType.Undefined;
                                    }

                                    chunkA.data[tileId] = tile;
                                }

                                // replice merge result to chunkB
                                chunkB.data.CopyFrom(chunkA.data);
                            }
                        }
                    }
                }
            }

            public override IEnumerator<State> execute(Generator.Context context)
            {
                var numChunks               = context.level.numChunks;
        
                // pre-compute random values, since we cannot pass mamanged Random instance to job
                var pathDisplacements       = new NativeArray<float>(Enumerable.Range(0, numChunks).Select(i => (((float)context.random.NextDouble() - 0.5f) * this.settings.walkablePathDisplacement) + 0.5f).ToArray(), Allocator.Persistent);

                var job                     = new CreateWallsJob
                {
                    mapChunkWallSize        = this.settings.mapChunkWallSize,
                    mapPathThickness        = this.settings.walkablePathThickness,
                    pathDisplacements       = pathDisplacements,
                    pathSteps               = context.level.pathSteps,
                    chunks                  = context.level.chunks

                };
                var dependsOn = context.schedule(job, numChunks, 8);

                var mergeJob                = new MergeOverlappingChunksJob
                {
                    chunks                  = context.level.chunks,
                };
                context.schedule(mergeJob, dependsOn);

                yield return State.Default;

                pathDisplacements.Dispose();
            }

            public override void initialize(Generator.Context context)
            {
                int numChunks               = context.level.pathSteps.Length + 1;

                // create level chunks from previously generated path and pre-compute chunk bounds and data index.
                {
                    for(int chunkId = 0; chunkId < numChunks; chunkId++)
                    {
                        var chunkInfo           = new LevelData.Chunk(chunkId);
                        var chunkWidth          = this.settings.mapChunkDimensions.x;
                        var chunkHeight         = this.settings.mapChunkDimensions.y;

                        if(chunkId > 0)
                        {
                            var last            = context.level.getChunk(chunkId - 1);
                            var from            = -context.level.pathSteps[chunkId - 1];

                            chunkInfo.bounds    = new RectInt
                            {
                                x               = from.x != 0
                                    ? from.x == -1
                                        // came from left
                                        ? last.bounds.x + chunkWidth
                                        // came from right
                                        : last.bounds.x - chunkWidth
                                    : last.bounds.x,
                                y               = from.y != 0
                                    ? from.y == -1
                                        // came from bottom
                                        ? last.bounds.y + chunkHeight
                                        // came from top
                                        : last.bounds.y - chunkHeight
                                    : last.bounds.y,
                                width           = chunkWidth,
                                height          = chunkHeight
                            };
                        }
                        // first chunk
                        else
                        {
                            chunkInfo.bounds    = new RectInt(Vector2Int.zero, new Vector2Int(chunkWidth, chunkHeight));
                        }

                        chunkInfo.dataSize      = chunkInfo.bounds.width * chunkInfo.bounds.height;
                        context.level.addChunk(ref chunkInfo);

                        //Debug.Log($"Chunk[{chunkId}]: {chunkInfo.bounds} (Index0: {chunkInfo.dataIndex0}, size: {chunkInfo.dataSize})");
                    }
                }
            }

            public override void release(Generator.Context context)
            {
            }
        }
    }
}

