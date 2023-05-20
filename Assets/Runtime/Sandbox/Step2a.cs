using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Transforms;
using UnityEngine;
using Unity.Jobs;

public class Step2a : MapGenStep<Step2aSettings>
{
    public override IEnumerator<MapGenStepState> execute(MapGeneratorSettings context)
    {
        for(int i = 0; i < context.data.numMapChunks; i++)
        {
            var chunkInfo   = context.data.getChunkInfo(i);
            var chunkData   = context.data.getChunkData(i);

            Vector2Int from = context.data.getChunkInfo(0).bounds.size / 2;
            if(i > 0)
            {
                var lastChunkInfo = context.data.getChunkInfo(i - 1);
                var offset = lastChunkInfo.bounds.position - chunkInfo.bounds.position;
                offset += new Vector2Int(offset.x != 0 ? -(int)Mathf.Sign(offset.x) : 0, offset.y != 0 ? -(int)Mathf.Sign(offset.y) : 0);

                from = lastChunkInfo.pathExit + offset;
            }

             var tileId = (from.y * chunkInfo.bounds.width) + from.x;
             var tileData = chunkData[tileId];

             tileData.constructionType = TileConstructionType.Walkable;
             chunkData[tileId] = tileData;

            Vector2Int to   = chunkInfo.pathExit;

            var currentPos = from;
            while(currentPos != to)
            {
                var options = this.walkOptions(currentPos, chunkInfo, chunkData);
                currentPos = options[context.random.Next(0, options.Count)];

                tileId = (currentPos.y * chunkInfo.bounds.width) + currentPos.x;
                tileData = chunkData[tileId];

                tileData.constructionType = TileConstructionType.Walkable;
                chunkData[tileId] = tileData;
            }

            var camefrom = i > 0 ? -context.data.pathSteps[i - 1]  : Vector2Int.zero;

            if(camefrom == Vector2Int.up)
            {
                for(int x = chunkInfo.wallSize; x < chunkInfo.bounds.width - chunkInfo.wallSize; x++)
                {
                    if(x == from.x) { continue; }

                    tileId = ((chunkInfo.bounds.height - 1) * chunkInfo.bounds.width) + x;
                    tileData = chunkData[tileId];

                    if(tileData.constructionType == TileConstructionType.Walkable)
                    {
                        tileData.constructionType = TileConstructionType.Undefined;
                        chunkData[tileId] = tileData;
                    }
                }
            }
            else if(camefrom == Vector2Int.down)
            {
                for(int x = chunkInfo.wallSize; x < chunkInfo.bounds.width - chunkInfo.wallSize; x++)
                {
                    if(x == from.x) { continue; }

                    tileId = x;
                    tileData = chunkData[tileId];

                    if(tileData.constructionType == TileConstructionType.Walkable)
                    {
                        tileData.constructionType = TileConstructionType.Undefined;
                        chunkData[tileId] = tileData;
                    }
                }
            }
            else if(camefrom == Vector2Int.left)
            {
                for(int y = chunkInfo.wallSize; y < chunkInfo.bounds.height - chunkInfo.wallSize; y++)
                {
                    if(y == from.y) { continue; }

                    tileId = (y * chunkInfo.bounds.width);
                    tileData = chunkData[tileId];

                    if(tileData.constructionType == TileConstructionType.Walkable)
                    {
                        tileData.constructionType = TileConstructionType.Undefined;
                        chunkData[tileId] = tileData;
                    }
                }
            }
            else if(camefrom == Vector2Int.right)
            {
                for(int y = chunkInfo.wallSize; y < chunkInfo.bounds.height - chunkInfo.wallSize; y++)
                {
                    if(y == from.y) { continue; }

                    tileId = (y * chunkInfo.bounds.width) + chunkInfo.bounds.width - 1;
                    tileData = chunkData[tileId];

                    if(tileData.constructionType == TileConstructionType.Walkable)
                    {
                        tileData.constructionType = TileConstructionType.Undefined;
                        chunkData[tileId] = tileData;
                    }
                }
            }
        }

        yield return new MapGenStepState {};
    }

    private List<Vector2Int> walkOptions(in Vector2Int current, in MapChunkInfo info, in NativeSlice<MapChunkData> data)
    {
        var options = new List<Vector2Int>(4);

        // walk left
        var walkLeft = current + Vector2Int.left;
        if(walkLeft.x >= 0 && data[(walkLeft.y * info.bounds.width) + walkLeft.x].constructionType != TileConstructionType.Obstructed)
            options.Add(walkLeft);

        // walk right
        var walkRight = current + Vector2Int.right;
        if(walkRight.x < info.bounds.width && data[(walkRight.y * info.bounds.width) + walkRight.x].constructionType != TileConstructionType.Obstructed)
            options.Add(walkRight);

        // walk top
        var walkTop = current + Vector2Int.up;
        if(walkTop.y < info.bounds.height && data[(walkTop.y * info.bounds.width) + walkTop.x].constructionType != TileConstructionType.Obstructed)
            options.Add(walkTop);

        // walk bottom
        var walkBottom = current + Vector2Int.down;
        if(walkBottom.y >= 0 && data[(walkBottom.y * info.bounds.width) + walkBottom.x].constructionType != TileConstructionType.Obstructed)
            options.Add(walkBottom);

        return options;
    }

    static List<List<T>> partition<T>(List<T> source, int n)
    {
        if(n == 0) { return new List<List<T>> { source }; }

        return source
            .Select((num, index) => new { Number = num, Index = index / n })
            .GroupBy(x => x.Index)
            .Select(group => group.Select(x => x.Number).ToList())
            .ToList();
    }

    public override void initialize(MapGeneratorSettings context)
    {
    }

    public override void release(MapGeneratorSettings context)
    {
    }

    public override void Dispose()
    {
    }
}
