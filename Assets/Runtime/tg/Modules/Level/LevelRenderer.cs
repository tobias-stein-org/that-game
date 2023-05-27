using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Entities;


namespace tg.level
{
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(LevelGeneratorSystem))]
    public partial class LevelRendererSystem : SystemBase
    {
        private LevelRenderData renderData;

        protected override void OnStartRunning()
        {
            if(SystemAPI.ManagedAPI.TryGetSingleton<LevelRenderData>(out var data))
            {
                this.renderData = data;
            }
        }

        protected override void OnUpdate()
        {
            var destroy = new List<Entity>();
            foreach(var (data, entity) in SystemAPI.Query<RenderLevelData>().WithEntityAccess())
            {
                destroy.Add(entity);

                if(this.renderData == null) { continue; }
                for(int chunkId = 0; chunkId < data.levelData.numChunks; chunkId++)
                {
                    var chunk = data.levelData.getChunk(chunkId);
                    for(int y = 0; y < chunk.bounds.height; y++)
                    for(int x = 0; x < chunk.bounds.width; x++)
                    {
                        var chunkTileId = (y * chunk.bounds.width) + x;
                        var chunkTile   = chunk.data[chunkTileId];
                        var tilePos     = new Vector3Int(chunk.bounds.x + x, -(chunk.bounds.y + y), 0);
                        foreach(var (layer, tilemap) in this.renderData.layers)
                        {
                            var module = chunkTile[layer].id;
                            if(module != LevelData.Module.INVALID)
                            {
                                tilemap.SetTile(tilePos, data.modules[module]);
                            }
                        }
                    }
                }
            }

            foreach(var e in destroy) { this.EntityManager.DestroyEntity(e); }
        }
    }

    public class RenderLevelData : IComponentData
    {
        public LevelData        levelData;
        public List<Module>     modules;
    }

    public class LevelRenderData : IComponentData
    {
        public Dictionary<int, Tilemap> layers;
    }

    public class LevelRenderer : MonoBehaviour
    {
        public class Baker : Baker<LevelRenderer>
        {
            public override void Bake(LevelRenderer authoring)
            {
                if(!Application.isPlaying) { return; }

                var layers  = new Dictionary<int, Tilemap>(LevelData.Layer.MAX_LAYERS);

                var gridGO = new GameObject("Level.Grid");
                {
                    var grid = gridGO.AddComponent<Grid>();
                    grid.cellSize *= 0.16f;

                    foreach(var layer in new[] { LevelData.Layer.Floor, LevelData.Layer.Obstructable })
                    {
                        var layerGO = new GameObject($"Layer.{(int)layer}");
                        {
                            layerGO.transform.SetParent(gridGO.transform);
                            var renderer = layerGO.AddComponent<TilemapRenderer>();
                            renderer.sortingOrder = layer;

                            layers.Add(layer, layerGO.GetComponent<Tilemap>());
                        }
                    }
                }

                var entity  = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new LevelRenderData
                {
                    layers  = layers,
                });
            }
        }
    }
}
