using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using UnityEngine.UIElements;
using UnityEngine.Tilemaps;
using System.Linq;
using Unity.Jobs;

public enum TileMask
{
    Undefined = 0,
    Walkable,
    Wall
}
public partial struct MapGenData
{
    public NativeArray<TileMask> walkableTilemapMask;
}

public class Step2 : MapGenStep<Step2Settings>
{

    public override void Dispose()
    {
    }


    private struct CreateMapChunkMaskJob : IJobParallelFor
    {
        public Vector2Int                           mapChunkDimensions;
        public int                                  mapChunkWallSize;
        public int                                  mapPathThickness;

        [ReadOnly]
        public NativeArray<Vector2Int>              pathSteps;

        [ReadOnly]
        public NativeArray<float>                   pathDisplacements;

        [NativeDisableParallelForRestriction] 
        public NativeArray<TileMask>                walkableTilemapMask;

        public void Execute(int chunkId)
        {
            var from = chunkId > 0
            ? -this.pathSteps[chunkId - 1]
            : Vector2Int.zero;

            var to = chunkId < this.pathSteps.Length
                ? this.pathSteps[chunkId]
                : Vector2Int.zero;

            var chunkSize   = this.mapChunkDimensions.x * this.mapChunkDimensions.y;
            var chunk = this.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize);

            // draw walls
            for(int y = 0; y < this.mapChunkDimensions.y; y++)
            {
                for(int x = 0; x < this.mapChunkDimensions.x; x++)
                {
                    var tileId = (y * this.mapChunkDimensions.x) + x;

                    // left wall
                    if(x < this.mapChunkWallSize && from != Vector2Int.left && to != Vector2Int.left)
                    {
                        chunk[tileId] = TileMask.Wall;
                    }

                    // right wall
                    if(x >= (this.mapChunkDimensions.x - this.mapChunkWallSize) && from != Vector2Int.right && to != Vector2Int.right)
                    {
                        chunk[tileId] = TileMask.Wall;
                    }

                    // top wall
                    if(y >= (this.mapChunkDimensions.y - this.mapChunkWallSize) && from != Vector2Int.up && to != Vector2Int.up)
                    {
                        chunk[tileId] = TileMask.Wall;
                    }

                    // bottom wall
                    if(y < this.mapChunkWallSize && from != Vector2Int.down && to != Vector2Int.down)
                    {
                        chunk[tileId] = TileMask.Wall;
                    }
                }
            }

            // draw path

            var displacement = this.pathDisplacements[chunkId];
            if(to == Vector2Int.left)
            {

                var pathStart = Mathf.RoundToInt((float)(this.mapChunkDimensions.y - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                {
                    var tileId = y * this.mapChunkDimensions.x;
                    chunk[tileId] = TileMask.Walkable;
                }
            }
            else if(to == Vector2Int.right)
            {
                var pathStart = Mathf.RoundToInt((float)(this.mapChunkDimensions.y - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                {
                    var tileId = (y * this.mapChunkDimensions.x) + (this.mapChunkDimensions.x - 1);
                    chunk[tileId] = TileMask.Walkable;
                }
            }
            else if(to == Vector2Int.up)
            {
                var pathStart = Mathf.RoundToInt((float)(this.mapChunkDimensions.x - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                {
                    var tileId = ((this.mapChunkDimensions.y - 1) * this.mapChunkDimensions.x) + x;
                    chunk[tileId] = TileMask.Walkable;
                }
            }
            else if(to == Vector2Int.down)
            {
                var pathStart = Mathf.RoundToInt((float)(this.mapChunkDimensions.x - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                {
                    var tileId = x;
                    chunk[tileId] = TileMask.Walkable;
                }
            }
        }
    }

    public override IEnumerator<MapGenStepState> execute(MapGenContext context)
    {
        var numChunks           = context.data.pathSteps.Length + 1;

        var rng                 = new System.Random();
        

        var pathDisplacements   = new NativeArray<float>(Enumerable.Range(0, numChunks).Select(i => (((float)rng.NextDouble() - 0.5f) * this.settings.walkablePathDisplacement) + 0.5f).ToArray(), Allocator.Persistent);

        var jobHandle           = new CreateMapChunkMaskJob
        {
            mapChunkDimensions  = this.settings.mapChunkDimensions,
            mapChunkWallSize    = this.settings.mapChunkWallSize,
            mapPathThickness    = this.settings.walkablePathThickness,
            pathDisplacements   = pathDisplacements,
            pathSteps           = context.data.pathSteps,
            walkableTilemapMask = context.data.walkableTilemapMask

        }.Schedule(numChunks, 8);

        while(!jobHandle.IsCompleted) { yield return new MapGenStepState {}; }
        jobHandle.Complete();

        pathDisplacements.Dispose();
    }

    public override void initialize(MapGenContext context)
    {
        int numChunks = context.data.pathSteps.Length + 1;
        int chunkSize = this.settings.mapChunkDimensions.x * this.settings.mapChunkDimensions.y;

        context.data.walkableTilemapMask = new NativeArray<TileMask>(Enumerable.Range(0, chunkSize * numChunks).Select(x => TileMask.Undefined).ToArray(), Allocator.Persistent);
    }

    public override void release(MapGenContext context)
    {
        if(context.data.walkableTilemapMask.IsCreated)
        {
            context.data.walkableTilemapMask.Dispose();
        }
    }
}
