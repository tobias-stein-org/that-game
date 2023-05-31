using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;
using Unity.Entities;
using Unity.Jobs;

namespace tg.level
{
    using System.Runtime.InteropServices;
    using tg.events;
    using tg.level.events;
    using static tg.level.LevelData;

    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(LevelGeneratorSystem))]
    public partial class LevelRendererSystem : SystemBase, IEventListener<LevelRendererSystem>
    {
        private LevelRenderData renderData;

        protected override void OnStartRunning()
        {
            if(SystemAPI.ManagedAPI.TryGetSingleton<LevelRenderData>(out var data))
            {
                this.renderData = data;
            }

            EventQueue.subscribe(this);
        }

        protected override void OnDestroy()
        {
            EventQueue.unsubscribe(this);
        }
        protected override void OnUpdate()
        {
            // nothing to do at the moment, so prevent this system from further updating.
            this.Enabled = false;
        }


        /// <summary>
        /// Repaint entire tilemap.
        /// </summary>
        /// <param name="e"></param>
        private void onRequestRenderLevelEvent(RequestRenderLevelEvent e)
        {
            this.renderTilemap(e.levelData, e.modules);
        }

        private void renderTilemap(in LevelData levelData, in List<Module> modules)
        {
            for(int chunkId = 0; chunkId < levelData.numChunks; chunkId++)
            {
                var chunk = levelData.getChunk(chunkId);
                for(int y = 0; y < chunk.bounds.height; y++)
                for(int x = 0; x < chunk.bounds.width; x++)
                {
                    var tileId  = (y * chunk.bounds.width) + x;
                    var tile    = chunk[tileId];
                    var tilePos = new Vector3Int(chunk.bounds.x + x, chunk.bounds.y + y, 0);
                    foreach(var (layer, tilemap) in this.renderData.layers)
                    {
                        var module = tile[layer].id;
                        if(module != LevelData.Module.INVALID)
                        {
                            tilemap.SetTile(tilePos, modules[module]);
                        }
                    }
                }
            }
        }
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
