using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Transforms;
using UnityEngine;

public class Step2a : MapGenStep<Step2aSettings>
{
    public override IEnumerator<MapGenStepState> execute(MapGeneratorSettings context)
    {
        for(int i = 0; i < context.data.mapChunkInfo.Length; i++)
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

            Vector2Int to   = chunkInfo.pathExit;

            Vector2Int diff = to - from;

            var sX = Enumerable.Range(0, Mathf.Abs(diff.x)).Select(s => Vector2Int.right    * (int)Mathf.Sign(diff.x)).ToList();
            var sY = Enumerable.Range(0, Mathf.Abs(diff.y)).Select(s => Vector2Int.up       * (int)Mathf.Sign(diff.y)).ToList();

            var spX = sX.Count / Mathf.Max(Mathf.FloorToInt((float)sX.Count * this.settings.pathConvolution), 1);
            var spY = sY.Count / Mathf.Max(Mathf.FloorToInt((float)sY.Count * this.settings.pathConvolution), 1);

            var pX = partition(sX, spX);
            var pY = partition(sY, spY);

            var shuffledSteps = pX.Concat(pY).OrderBy(_ => context.random.Next()).SelectMany(partition => partition).ToList();

            var S = from;
            foreach(var s in shuffledSteps)
            { 
                var tileId = (S.y * chunkInfo.bounds.width) + S.x;
                var tileData = chunkData[tileId];

                tileData.constructionType = TileConstructionType.Walkable;
                chunkData[tileId] = tileData;

                S += s;
            }

            from = to;
        }

        yield return new MapGenStepState {};
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
