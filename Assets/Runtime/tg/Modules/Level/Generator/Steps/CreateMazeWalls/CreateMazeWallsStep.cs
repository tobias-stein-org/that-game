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
    namespace generator.step
    {
        public class CreateMazeWallsStep : LevelGeneratorStep<CreateMazeWallsStepSettings, CreateMazeStep.State, CreateMazeWallsStep.State>
        {
            public class State : StateBase
            {
                public Vector2Int[] chunkExits;
                public int          chunkWallSize;

                internal static new State Default => new State {};
            }

            /// <summary>
            /// Each parallel job will create walls surrounding the currently processed level chunk.
            /// No walls will be created on those side two chunks connect.
            /// </summary>
            private struct CreateWallsJob : IJobParallelFor
            {
                public int                                  mapChunkWallSize;
                public int                                  mapPathThickness;

                [ReadOnly]
                public NativeArray<float>                   pathDisplacements;

                [NativeDisableParallelForRestriction] 
                public UnsafeList<LevelData.Chunk>          chunks;

                [NativeDisableParallelForRestriction]
                public NativeArray<Vector2Int>              chunkExits;

                public void Execute(int chunkIndex)
                {
                    var from        = chunkIndex > 0                        ? this.chunks[chunkIndex - 1].bounds.position - this.chunks[chunkIndex].bounds.position : Vector2Int.zero;
                    var to          = chunkIndex < this.chunks.Length - 1   ? this.chunks[chunkIndex + 1].bounds.position - this.chunks[chunkIndex].bounds.position : Vector2Int.zero;

                    from            = new Vector2Int(Mathf.FloorToInt(from.x    / from.magnitude), Mathf.FloorToInt(from.y  / from.magnitude));
                    to              = new Vector2Int(Mathf.FloorToInt(to.x      / to.magnitude),   Mathf.FloorToInt(to.y    / to.magnitude));

                    ref var chunk   = ref this.chunks.ElementAt(chunkIndex);

                    // draw walls
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
                    this.chunkExits[chunkIndex] = chunk.bounds.size / 2;
                    var displacement = this.pathDisplacements[chunkIndex];
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

                        this.chunkExits[chunkIndex]              = new Vector2Int(0, pathStart + this.mapPathThickness / 2);
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

                        this.chunkExits[chunkIndex] = new Vector2Int(chunk.bounds.width - 1, pathStart + this.mapPathThickness / 2);
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

                        this.chunkExits[chunkIndex] = new Vector2Int(pathStart + this.mapPathThickness / 2, chunk.bounds.height - 1);
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

                        this.chunkExits[chunkIndex] = new Vector2Int(pathStart + this.mapPathThickness / 2, 0);
                    }

                    // set chunk neighbours
                    {
                        // previous chunk
                        if(chunkIndex > 0)
                        {
                            ref var prevChunk = ref this.chunks.ElementAt(chunkIndex - 1);

                                 if(from == Vector2Int.up)      { chunk.topNeighbour    = prevChunk.id; }
                            else if(from == Vector2Int.down)    { chunk.bottomNeighbour = prevChunk.id; }
                            else if(from == Vector2Int.left)    { chunk.leftNeighbour   = prevChunk.id; }
                            else if(from == Vector2Int.right)   { chunk.rightNeighbour  = prevChunk.id; }
                        }

                        // next chunk
                        if(chunkIndex < this.chunks.Length - 2)
                        {
                            ref var nextChunk = ref this.chunks.ElementAt(chunkIndex + 1);

                                 if(to == Vector2Int.up)        { chunk.topNeighbour    = nextChunk.id; }
                            else if(to == Vector2Int.down)      { chunk.bottomNeighbour = nextChunk.id; }
                            else if(to == Vector2Int.left)      { chunk.leftNeighbour   = nextChunk.id; }
                            else if(to == Vector2Int.right)     { chunk.rightNeighbour  = nextChunk.id; }
                        }
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
                    for(var chunkBId = chunkAId + 1; chunkBId < this.chunks.Length; chunkBId++)
                    {
                        var chunkA = this.chunks[chunkAId];
                        var chunkB = this.chunks[chunkBId];

                        // two different chunks overlap
                        if(chunkA.bounds.position == chunkB.bounds.position)
                        {
                            // merge all walkable tiles from chunkB with chunkA
                            for(int y = 0; y < chunkA.bounds.height; y++)
                            for(int x = 0; x < chunkA.bounds.width; x++)
                            {
                                var tileId = (y * chunkA.bounds.width) + x;
                                var tile = chunkA[tileId];

                                if(chunkB[tileId].constructionType == LevelData.Tile.ConstructionType.Walkable)
                                {
                                    tile.constructionType = LevelData.Tile.ConstructionType.Walkable;
                                }

                                if(chunkB[tileId].constructionType == LevelData.Tile.ConstructionType.Undefined && chunkA[tileId].constructionType == LevelData.Tile.ConstructionType.Obstructed)
                                {
                                    tile.constructionType = LevelData.Tile.ConstructionType.Undefined;
                                }

                                chunkA[tileId] = tile;
                            }

                            // replice merge result to chunkB
                            chunkB.data.CopyFrom(chunkA.data);

                            // sync neighbours
                            chunkA.neighbours = Unity.Mathematics.math.max(chunkA.neighbours, chunkB.neighbours);
                            chunkB.neighbours = chunkA.neighbours;
                        }
                    }
                }
            }

            public override IEnumerator<State> execute(Generator.Context context, CreateMazeStep.State lastStepState)
            {
                var numChunks               = this.createLevelChunks(context, lastStepState);
        
                // pre-compute random values, since we cannot pass mamanged Random instance to job
                var pathDisplacements       = new NativeArray<float>(Enumerable.Range(0, numChunks).Select(i => (((float)context.random.NextDouble() - 0.5f) * this.settings.walkablePathDisplacement) + 0.5f).ToArray(), Allocator.Persistent);

                var chunkExits              = new NativeArray<Vector2Int>(numChunks, Allocator.Persistent);
                var job                     = new CreateWallsJob
                {
                    mapChunkWallSize        = this.settings.mapChunkWallSize,
                    mapPathThickness        = this.settings.walkablePathThickness,
                    pathDisplacements       = pathDisplacements,
                    chunks                  = context.level.chunks,
                    chunkExits              = chunkExits

                };
                var dependsOn = context.schedule(job, numChunks, 8);

                var mergeJob                = new MergeOverlappingChunksJob
                {
                    chunks                  = context.level.chunks,
                };
                context.schedule(mergeJob, dependsOn);

                yield return State.Default;

                var output = new State {
                    chunkExits      = chunkExits.ToArray(),
                    chunkWallSize   = this.settings.mapChunkWallSize
                };

                pathDisplacements.Dispose();
                chunkExits.Dispose();

                yield return output;
            }

            private int createLevelChunks(Generator.Context context, CreateMazeStep.State input)
            {
                int numChunks               = input.pathSteps.Count + 1;
                // create level chunks from previously generated path and pre-compute chunk bounds and data index.
                {
                    for(int chunkId = 0; chunkId < numChunks; chunkId++)
                    {
                        var chunkX              = 0;
                        var chunkY              = 0;

                        var chunkWidth          = this.settings.mapChunkDimensions.x;
                        var chunkHeight         = this.settings.mapChunkDimensions.y;

                        if(chunkId > 0)
                        {
                            var last            = context.level.getChunk(chunkId - 1);
                            var from            = -input.pathSteps[chunkId - 1];

                            chunkX              = from.x != 0
                                    ? from.x == -1
                                        // came from left
                                        ? last.bounds.x + chunkWidth
                                        // came from right
                                        : last.bounds.x - chunkWidth
                                    : last.bounds.x;

                            chunkY               = from.y != 0
                                    ? from.y == -1
                                        // came from bottom
                                        ? last.bounds.y + chunkHeight
                                        // came from top
                                        : last.bounds.y - chunkHeight
                                    : last.bounds.y;
                        }

                        context.level.createNewChunk(chunkX, chunkY, chunkWidth, chunkHeight);
                        //Debug.Log($"Chunk[{chunkId}]: {chunkInfo.bounds} (Index0: {chunkInfo.dataIndex0}, size: {chunkInfo.dataSize})");
                    }
                }

                return numChunks;
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

