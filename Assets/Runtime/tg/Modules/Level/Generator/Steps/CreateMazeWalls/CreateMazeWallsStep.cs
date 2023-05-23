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
        public NativeArray<ChunkInfo>   chunkInfo;
        public NativeArray<Tile>        chunkData;

        public int                      numChunks { get { return this.chunkInfo.Length; } }

        #region Convenient methods

        public ChunkInfo?               getChunkInfo(Vector2Int position)
        {
            foreach(var chunkInfo in this.chunkInfo)
            {
                if(chunkInfo.bounds.Contains(position))
                {
                    return chunkInfo;
                }
            }

            return null;
        }

        public ChunkInfo                getChunkInfo(int chunkId) { return this.chunkInfo[chunkId]; }
        public NativeSlice<Tile>        getChunkData(int chunkId)
        {
            var chunkInfo = this.chunkInfo[chunkId];
            return this.chunkData.Slice(chunkInfo.dataIndex0, chunkInfo.dataSize);
        }

        #endregion
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
                public NativeArray<LevelData.Tile>          chunkData;

                [NativeDisableParallelForRestriction] 
                public NativeArray<LevelData.ChunkInfo>     chunkInfo;

                public void Execute(int chunkId)
                {
                    var from        = chunkId > 0                       ? -this.pathSteps[chunkId - 1]  : Vector2Int.zero;
                    var to          = chunkId < this.pathSteps.Length   ? this.pathSteps[chunkId]       : Vector2Int.zero;

                    var chunkInfo   = this.chunkInfo[chunkId];
                    var chunkData   = this.chunkData.Slice(chunkInfo.dataIndex0, chunkInfo.dataSize);

                    // draw walls
                    chunkInfo.wallSize = this.mapChunkWallSize;
                    for(int y = 0; y < chunkInfo.bounds.height; y++)
                    {
                        for(int x = 0; x < chunkInfo.bounds.width; x++)
                        {
                            var tileId  = (y * chunkInfo.bounds.width) + x;
                            var tile    = chunkData[tileId];

                            // top-left
                            if(x < this.mapChunkWallSize && y < this.mapChunkWallSize)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }
                            // top-right
                            else if(x > chunkInfo.bounds.width - this.mapChunkWallSize - 1 && y < this.mapChunkWallSize)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }
                            // bottom-left
                            else if(x < this.mapChunkWallSize && y > chunkInfo.bounds.height - this.mapChunkWallSize - 1)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }
                            // bottom-right
                            else if(x > chunkInfo.bounds.width - this.mapChunkWallSize - 1 && y > chunkInfo.bounds.height - this.mapChunkWallSize - 1)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }
                            // left wall
                            else if(x < this.mapChunkWallSize && from != Vector2Int.left && to != Vector2Int.left)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }

                            // right wall
                            else if(x >= (chunkInfo.bounds.width - this.mapChunkWallSize) && from != Vector2Int.right && to != Vector2Int.right)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }

                            // top wall
                            else if(y >= (chunkInfo.bounds.height - this.mapChunkWallSize) && from != Vector2Int.up && to != Vector2Int.up)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }

                            // bottom wall
                            else if(y < this.mapChunkWallSize && from != Vector2Int.down && to != Vector2Int.down)
                            {
                                tile.constructionType = LevelData.Tile.ConstructionType.Obstructed;
                            }

                            chunkData[tileId] = tile;
                        }
                    }

                    // draw path
                    chunkInfo.pathExit = chunkInfo.bounds.size / 2;
                    var displacement = this.pathDisplacements[chunkId];
                    if(to == Vector2Int.left)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.height - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                        {
                            var tileId = y * chunkInfo.bounds.width;
                            var tile = chunkData[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunkData[tileId] = tile;
                        }

                        chunkInfo.pathExit = new Vector2Int(0, pathStart + this.mapPathThickness / 2);
                    }
                    else if(to == Vector2Int.right)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.height - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                        {
                            var tileId = (y * chunkInfo.bounds.width) + (chunkInfo.bounds.width - 1);
                            var tile = chunkData[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunkData[tileId] = tile;
                        }

                        chunkInfo.pathExit = new Vector2Int(chunkInfo.bounds.width - 1, pathStart + this.mapPathThickness / 2);
                    }
                    else if(to == Vector2Int.up)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.width - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                        {
                            var tileId = ((chunkInfo.bounds.height - 1) * chunkInfo.bounds.width) + x;
                            var tile = chunkData[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunkData[tileId] = tile;
                        }

                        chunkInfo.pathExit = new Vector2Int(pathStart + this.mapPathThickness / 2, chunkInfo.bounds.height - 1);
                    }
                    else if(to == Vector2Int.down)
                    {
                        var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.width - (2 * (this.mapChunkWallSize + 1)) - this.mapPathThickness) * displacement) + this.mapChunkWallSize + 1;
                        for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                        {
                            var tileId = x;
                            var tile = chunkData[tileId];

                            tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                            chunkData[tileId] = tile;
                        }

                        chunkInfo.pathExit = new Vector2Int(pathStart + this.mapPathThickness / 2, 0);
                    }

                    // update chunk info
                    this.chunkInfo[chunkId]  = chunkInfo;
                }
            }

            /// <summary>
            /// This job ensures that overlapping chunks are gettings merged. This means wall openings
            /// are getting replicated.
            /// </summary>
            internal struct MergeOverlappingChunksJob : IJob
            {
                public NativeArray<LevelData.Tile>      chunkData;
                public NativeArray<LevelData.ChunkInfo> chunkInfo;

                public void Execute()
                {
                    for(int chunkA = 0; chunkA < this.chunkInfo.Length; chunkA++)
                    {
                        for(int chunkB = chunkA; chunkB < this.chunkInfo.Length; chunkB++)
                        {
                            if(chunkA == chunkB) { continue; }

                            var infoA = this.chunkInfo[chunkA];
                            var infoB = this.chunkInfo[chunkB];

                            // two different chunks overlap
                            if(infoA.bounds.position == infoB.bounds.position)
                            {
                                var dataA = this.chunkData.Slice(infoA.dataIndex0, infoA.dataSize);
                                var dataB = this.chunkData.Slice(infoB.dataIndex0, infoB.dataSize);

                                // merge all walkable tiles from chunkB with chunkA
                                for(int y = 0; y < infoA.bounds.height; y++)
                                for(int x = 0; x < infoA.bounds.height; x++)
                                {
                                    var tileId = (y * infoA.bounds.width) + x;
                                    var tile = dataA[tileId];

                                    if(dataB[tileId].constructionType == LevelData.Tile.ConstructionType.Walkable)
                                    {
                                        tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                                    }

                                    if(dataB[tileId].constructionType == LevelData.Tile.ConstructionType.Undefined && dataA[tileId].constructionType == LevelData.Tile.ConstructionType.Obstructed)
                                    {
                                        tile.constructionType = LevelData.Tile.ConstructionType.Undefined;
                                    }

                                    dataA[tileId] = tile;
                                }

                                // replice merge result to chunkB
                                dataB.CopyFrom(dataA);
                            }
                        }
                    }
                }
            }

            public override IEnumerator<State> execute(Generator.Context context)
            {
                var numChunks               = context.data.numChunks;
        
                // pre-compute random values, since we cannot pass mamanged Random instance to job
                var pathDisplacements       = new NativeArray<float>(Enumerable.Range(0, numChunks).Select(i => (((float)context.random.NextDouble() - 0.5f) * this.settings.walkablePathDisplacement) + 0.5f).ToArray(), Allocator.Persistent);

                var job                     = new CreateWallsJob
                {
                    mapChunkWallSize        = this.settings.mapChunkWallSize,
                    mapPathThickness        = this.settings.walkablePathThickness,
                    pathDisplacements       = pathDisplacements,
                    pathSteps               = context.data.pathSteps,
                    chunkData               = context.data.chunkData,
                    chunkInfo               = context.data.chunkInfo

                };
                var dependsOn = context.schedule(job, numChunks, 8);

                var mergeJob                = new MergeOverlappingChunksJob
                {
                    chunkInfo               = context.data.chunkInfo,
                    chunkData               = context.data.chunkData,
                };
                context.schedule(mergeJob, dependsOn);

                yield return State.Default;

                pathDisplacements.Dispose();
            }

            public override void initialize(Generator.Context context)
            {
               
                var nextChunkDataIndex0     = 0;
                int numChunks               = context.data.pathSteps.Length + 1;
                context.data.chunkInfo      = new NativeArray<LevelData.ChunkInfo>(numChunks, Allocator.Persistent);

                // create level chunks from previously generated path and pre-compute chunk bounds and data index.
                {
                    for(int chunkId = 0; chunkId < numChunks; chunkId++)
                    {
                        var chunkInfo           = new LevelData.ChunkInfo(chunkId);
                        var chunkWidth          = this.settings.mapChunkDimensions.x;
                        var chunkHeight         = this.settings.mapChunkDimensions.y;

                        chunkInfo.dataIndex0    = nextChunkDataIndex0;

                        if(chunkId > 0)
                        {
                            var last            = context.data.chunkInfo[chunkId - 1];
                            var from            = context.data.pathSteps[chunkId - 1];

                            chunkInfo.bounds    = new RectInt
                            {
                                x               = from.x != 0
                                    ? from.x == -1
                                        // came from right
                                        ? last.bounds.x - chunkWidth
                                        // came from left
                                        : last.bounds.x + last.bounds.width
                                    : last.bounds.x,
                                y               = from.y != 0
                                    ? from.y == -1
                                        // came from bottom
                                        ? last.bounds.y - chunkHeight
                                        // came from top
                                        : last.bounds.y + last.bounds.height
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
                        nextChunkDataIndex0     += chunkInfo.dataSize;

                        context.data.chunkInfo[chunkId] = chunkInfo;

                        //Debug.Log($"Chunk[{chunkId}]: {chunkInfo.bounds} (Index0: {chunkInfo.dataIndex0}, size: {chunkInfo.dataSize})");
                    }
                }

                // allocate enough memory to store all level tiles of each chunk
                context.data.chunkData          = new NativeArray<LevelData.Tile>(Enumerable.Range(0, nextChunkDataIndex0).Select(x => LevelData.Tile.Default).ToArray(), Allocator.Persistent);
            }

            public override void release(Generator.Context context)
            {
                if(context.data.chunkData.IsCreated)
                {
                    for(int chunkId = 0; chunkId < context.data.numChunks; chunkId++)
                    {
                        context.data.chunkData[chunkId].Dispose();
                    }

                    context.data.chunkData.Dispose();
                }

                if(context.data.chunkInfo.IsCreated) { context.data.chunkInfo.Dispose(); }
            }
        }
    }
}

