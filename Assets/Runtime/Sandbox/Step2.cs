using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using static UnityEngine.Rendering.DebugUI.Table;
using Unity.Entities.UniversalDelegates;
using UnityEngine.UIElements;
using UnityEngine.Tilemaps;
using System.Linq;

public enum TileMask
{
    Undefined = 0,
    Walkable,
    Wall
}
public partial struct MapGenData
{
    public NativeArray<NativeArray<TileMask>> walkableTilemapMask;
}

public class Step2 : MapGenStep<Step2Settings>
{
    public override void Dispose()
    {
    }


    public override void execute(ref MapGenContext context)
    {
        for(int chunkId = 0; chunkId < context.data.pathSteps.Length + 1; chunkId++)
        {
            this.drawWalls(ref context, chunkId);
        }
    }

    private void drawWalls(ref MapGenContext context, int chunkId)
    {
        var from    = chunkId > 0
            ? -context.data.pathSteps[chunkId - 1]
            : Vector2Int.zero;

        var to      = chunkId < context.data.pathSteps.Length
            ? context.data.pathSteps[chunkId]
            : Vector2Int.zero;

        var chunk = context.data.walkableTilemapMask[chunkId];

        for(int y = 0; y < this.settings.mapChunckDimensions.y; y++)
        {
            for(int x = 0; x < this.settings.mapChunckDimensions.x; x++)
            {
                var tileId = (y * this.settings.mapChunckDimensions.x) + x;

                // left wall
                if(x < this.settings.mapChunkWallSize && from != Vector2Int.left && to != Vector2Int.left)
                {
                    chunk[tileId] = TileMask.Wall;
                }

                // right wall
                if(x >= (this.settings.mapChunckDimensions.x - this.settings.mapChunkWallSize) && from != Vector2Int.right && to != Vector2Int.right)
                {
                    chunk[tileId] = TileMask.Wall;
                }

                // top wall
                if(y >= (this.settings.mapChunckDimensions.y - this.settings.mapChunkWallSize) && from != Vector2Int.up && to != Vector2Int.up)
                {
                    chunk[tileId] = TileMask.Wall;
                }

                // bottom wall
                if(y < this.settings.mapChunkWallSize && from != Vector2Int.down && to != Vector2Int.down)
                {
                    chunk[tileId] = TileMask.Wall;
                }
            }
        }
    }

    public override void initialize(ref MapGenContext context)
    {
        int numChunks = context.data.pathSteps.Length + 1;
        int chunkSize = this.settings.mapChunckDimensions.x * this.settings.mapChunckDimensions.y;

        context.data.walkableTilemapMask = new NativeArray<NativeArray<TileMask>>(numChunks, Allocator.Persistent);
        for(int chunkId = 0; chunkId < context.data.walkableTilemapMask.Length; chunkId++)
        {
            context.data.walkableTilemapMask[chunkId] = new NativeArray<TileMask>(Enumerable.Range(0, chunkSize).Select(x => TileMask.Undefined).ToArray(), Allocator.Persistent);
        }
    }

    public override void release(ref MapGenContext context)
    {
        if(context.data.walkableTilemapMask.IsCreated)
        {
            for(int chunkId = 0; chunkId < context.data.walkableTilemapMask.Length; chunkId++)
            {
                context.data.walkableTilemapMask[chunkId].Dispose();
            }
            context.data.walkableTilemapMask.Dispose();
        }
    }
}
