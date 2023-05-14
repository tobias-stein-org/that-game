using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using UnityEngine.UIElements;
using UnityEngine.Tilemaps;
using System.Linq;
using Unity.Jobs;


public struct MapChunkInfo
{
    public RectInt                          bounds;

    public int                              dataIndex0;
    public int                              dataSize;
}

public struct MapChunkData
{
    public int                              floorModuleId;
    public int                              obstrModuleId;
        
    public TileConstructionType             constructionType;

    public static MapChunkData Empty
    {
        get
        {
            return new MapChunkData
            {
                floorModuleId      = -1,
                obstrModuleId      = -1,
                constructionType   = TileConstructionType.Undefined
            };
        }
    }
}

public partial struct MapGenData
{
    public NativeArray<MapChunkInfo>        mapChunkInfo;
    public NativeArray<MapChunkData>        mapChunkData;

    public int                              numMapChunks { get { return this.mapChunkInfo.Length; } }

    public MapChunkInfo                     getChunkInfo(int chunkId) { return this.mapChunkInfo[chunkId]; }
    public NativeSlice<MapChunkData>        getChunkData(int chunkId)
    {
        var chunkInfo = this.mapChunkInfo[chunkId];
        return this.mapChunkData.Slice(chunkInfo.dataIndex0, chunkInfo.dataSize);
    }
}

public class Step2 : MapGenStep<Step2Settings>
{
    private struct CreateMapChunkMaskJob : IJobParallelFor
    {
        public int                                  mapChunkWallSize;
        public int                                  mapPathThickness;

        [ReadOnly]
        public NativeArray<Vector2Int>              pathSteps;

        [ReadOnly]
        public NativeArray<float>                   pathDisplacements;

        [NativeDisableParallelForRestriction] 
        public NativeArray<MapChunkData>            mapChunkData;

        [NativeDisableParallelForRestriction] 
        public NativeArray<MapChunkInfo>            mapChunkInfo;

        public void Execute(int chunkId)
        {
            var from        = chunkId > 0                       ? -this.pathSteps[chunkId - 1]  : Vector2Int.zero;
            var to          = chunkId < this.pathSteps.Length   ? this.pathSteps[chunkId]       : Vector2Int.zero;

            var chunkInfo   = this.mapChunkInfo[chunkId];
            var chunkData   = this.mapChunkData.Slice(chunkInfo.dataIndex0, chunkInfo.dataSize);

            // draw walls
            for(int y = 0; y < chunkInfo.bounds.height; y++)
            {
                for(int x = 0; x < chunkInfo.bounds.width; x++)
                {
                    var tileId  = (y * chunkInfo.bounds.width) + x;
                    var tile    = chunkData[tileId];

                    // left wall
                    if(x < this.mapChunkWallSize && from != Vector2Int.left && to != Vector2Int.left)
                    {
                        tile.constructionType = TileConstructionType.Obstructed;
                    }

                    // right wall
                    if(x >= (chunkInfo.bounds.width - this.mapChunkWallSize) && from != Vector2Int.right && to != Vector2Int.right)
                    {
                        tile.constructionType = TileConstructionType.Obstructed;
                    }

                    // top wall
                    if(y >= (chunkInfo.bounds.height - this.mapChunkWallSize) && from != Vector2Int.up && to != Vector2Int.up)
                    {
                        tile.constructionType = TileConstructionType.Obstructed;
                    }

                    // bottom wall
                    if(y < this.mapChunkWallSize && from != Vector2Int.down && to != Vector2Int.down)
                    {
                        tile.constructionType = TileConstructionType.Obstructed;
                    }

                    chunkData[tileId] = tile;
                }
            }

            // draw path

            var displacement = this.pathDisplacements[chunkId];
            if(to == Vector2Int.left)
            {

                var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.height - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                {
                    var tileId = y * chunkInfo.bounds.width;
                    var tile = chunkData[tileId];

                    tile.constructionType = TileConstructionType.Walkable;
                    chunkData[tileId] = tile;
                }
            }
            else if(to == Vector2Int.right)
            {
                var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.height - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int y = pathStart; y < pathStart + this.mapPathThickness; y++)
                {
                    var tileId = (y * chunkInfo.bounds.width) + (chunkInfo.bounds.width - 1);
                    var tile = chunkData[tileId];

                    tile.constructionType = TileConstructionType.Walkable;
                    chunkData[tileId] = tile;
                }
            }
            else if(to == Vector2Int.up)
            {
                var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.width - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                {
                    var tileId = ((chunkInfo.bounds.height - 1) * chunkInfo.bounds.width) + x;
                    var tile = chunkData[tileId];

                    tile.constructionType = TileConstructionType.Walkable;
                    chunkData[tileId] = tile;
                }
            }
            else if(to == Vector2Int.down)
            {
                var pathStart = Mathf.RoundToInt((float)(chunkInfo.bounds.width - (2 * this.mapChunkWallSize) - this.mapPathThickness) * displacement) + this.mapChunkWallSize;
                for(int x = pathStart; x < pathStart + this.mapPathThickness; x++)
                {
                    var tileId = x;
                    var tile = chunkData[tileId];

                    tile.constructionType = TileConstructionType.Walkable;
                    chunkData[tileId] = tile;
                }
            }
        }
    }

    public override IEnumerator<MapGenStepState> execute(MapGeneratorSettings context)
    {
        var numChunks           = context.data.numMapChunks;
        

        var pathDisplacements   = new NativeArray<float>(Enumerable.Range(0, numChunks).Select(i => (((float)context.random.NextDouble() - 0.5f) * this.settings.walkablePathDisplacement) + 0.5f).ToArray(), Allocator.Persistent);

        var jobHandle           = new CreateMapChunkMaskJob
        {
            mapChunkWallSize    = this.settings.mapChunkWallSize,
            mapPathThickness    = this.settings.walkablePathThickness,
            pathDisplacements   = pathDisplacements,
            pathSteps           = context.data.pathSteps,
            mapChunkData        = context.data.mapChunkData,
            mapChunkInfo        = context.data.mapChunkInfo

        }.Schedule(numChunks, 8);

        while(!jobHandle.IsCompleted) { yield return new MapGenStepState {}; }
        jobHandle.Complete();

        pathDisplacements.Dispose();
    }

    public override void initialize(MapGeneratorSettings context)
    {
        int numChunks = context.data.pathSteps.Length + 1;

        context.data.mapChunkInfo   = new NativeArray<MapChunkInfo>(numChunks, Allocator.Persistent);
        var nextChunkDataIndex0     = 0;

        for(int chunkId = 0; chunkId < numChunks; chunkId++)
        {
            var chunkInfo           = new MapChunkInfo();
            var chunkWidth          = this.settings.mapChunkDimensions.x;
            var chunkHeight         = this.settings.mapChunkDimensions.y;

            chunkInfo.dataIndex0    = nextChunkDataIndex0;

            if(chunkId > 0)
            {
                var last            = context.data.mapChunkInfo[chunkId - 1];
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

            context.data.mapChunkInfo[chunkId] = chunkInfo;

            //Debug.Log($"Chunk[{chunkId}]: {chunkInfo.bounds} (Index0: {chunkInfo.dataIndex0}, size: {chunkInfo.dataSize})");
        }

        context.data.mapChunkData = new NativeArray<MapChunkData>(Enumerable.Range(0, nextChunkDataIndex0).Select(x => MapChunkData.Empty).ToArray(), Allocator.Persistent);
    }

    public override void release(MapGeneratorSettings context)
    {
        if(context.data.mapChunkData.IsCreated) { context.data.mapChunkData.Dispose(); }
        if(context.data.mapChunkInfo.IsCreated) { context.data.mapChunkInfo.Dispose(); }
    }

    public override void Dispose()
    {
    }
}
