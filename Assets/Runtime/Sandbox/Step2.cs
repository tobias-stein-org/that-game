using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using static UnityEngine.Rendering.DebugUI.Table;
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

        [ReadOnly]
        public NativeArray<Vector2Int>              pathSteps;

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
        }
    }

    public override IEnumerator<MapGenStepState> execute(MapGenContext context)
    {

        var jobHandle = new CreateMapChunkMaskJob
        {
            mapChunkDimensions = this.settings.mapChunkDimensions,
            mapChunkWallSize    = this.settings.mapChunkWallSize,
            pathSteps           = context.data.pathSteps,
            walkableTilemapMask = context.data.walkableTilemapMask

        }.Schedule(context.data.pathSteps.Length + 1, 8);

        while(!jobHandle.IsCompleted) { yield return new MapGenStepState {}; }
        jobHandle.Complete();
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
