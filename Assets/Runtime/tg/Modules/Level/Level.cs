using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Tilemaps;

using Unity.Entities;

namespace tg.level
{
    using tg.events;
    using tg.level.generator;
    using tg.level.events;
    using tg.application.events;

    /// <summary>
    /// The Level behaviour acts as a manager to generate new levels and repaint the tilemap layers.
    /// </summary>
    public class Level : MonoBehaviour, IEventListener<Level>
    {
        public GeneratorSettings                        initialSettings;

        private Generator                               generator;
        private Coroutine                               executor;

        private Dictionary<LevelData.Layer, Tilemap>    layers;

        private EntityManager                           entityManager;


        void Awake()
        {
            this.entityManager      = World.DefaultGameObjectInjectionWorld.EntityManager;
            this.setupTilemap();

        }

        void Start()
        {
            this.initializeNewGenerator(this.initialSettings);
        }

        void OnEnable()
        {
            EventQueue.subscribe(this);
        }

        void OnDisable()
        {
            EventQueue.unsubscribe(this);
        }

        void OnDestroy()
        {
            this.disposeCurrentLevel();
        }

        private void setupTilemap()
        {
            this.layers = new Dictionary<LevelData.Layer, Tilemap>(LevelData.Layer.MAX_LAYERS);

            var gridGO  = new GameObject("Level");
            {
                var rb          = gridGO.AddComponent<Rigidbody2D>();
                {
                    rb.bodyType = RigidbodyType2D.Static;
                }

                gridGO.layer    = LayerMask.NameToLayer("Level");
                gridGO.tag      = "Level";

                var grid        = gridGO.AddComponent<Grid>();

                foreach(var layer in new[] { LevelData.Layer.Floor, LevelData.Layer.Obstructable })
                {
                    var layerGO = new GameObject($"Layer.{(int)layer}");
                    {
                        layerGO.layer    = LayerMask.NameToLayer("Level");
                        layerGO.tag      = "Level";
                        layerGO.transform.SetParent(gridGO.transform);

                        if(layer.collision)
                        {
                            layerGO.AddComponent<TilemapCollider2D>();
                        }

                        var renderer = layerGO.AddComponent<TilemapRenderer>();
                        {
                            renderer.sortingOrder = layer;
                        }

                        layers.Add(layer, layerGO.GetComponent<Tilemap>());
                    }
                }
            }
        }

        private void initializeNewGenerator(GeneratorSettings settings)
        {
            this.disposeCurrentLevel();

            this.generator = new Generator(settings);

            this.generator.onLevelGeneratorStarted += () => EventQueue.publish(new LevelGenerationStartedEvent {});

            this.generator.onLevelGeneratorFinished += (in LevelData data) =>
            {
                // expose level data to DOTS
                this.entityManager.CreateSingleton<LevelData>(data);

                EventQueue.publish(new NewLevelGeneratedEvent { levelData = data });
                this.renderTilemap(in data);
            };
        }

        private void disposeCurrentLevel()
        {
            if(World.DefaultGameObjectInjectionWorld != null && this.entityManager.CreateEntityQuery(typeof(LevelData)).TryGetSingletonEntity<LevelData>(out Entity levelData)) { this.entityManager.DestroyEntity(levelData); }

            if(this.executor != null)
            {
                this.StopCoroutine(this.executor);
                this.executor = null;

                this.generator?.Dispose();

                if(EventQueue.isValid) { EventQueue.publish(new LevelGenerationAbortedEvent {}); }
            }
        }

        private void startGenerator()
        {
            this.disposeCurrentLevel();
            this.executor = this.StartCoroutine(this.generator.execute());
        }

        private void renderTilemap(in LevelData levelData)
        {
            // clear previous tilemap rendering
            foreach(var (layer, tilemap) in this.layers) { tilemap.ClearAllTiles(); }

            for(int chunkId = 0; chunkId < levelData.numChunks; chunkId++)
            {
                var chunk = levelData.getChunk(chunkId);
                for(int y = 0; y < chunk.bounds.height; y++)
                for(int x = 0; x < chunk.bounds.width; x++)
                {
                    var tileId  = (y * chunk.bounds.width) + x;
                    var tile    = chunk[tileId];
                    var tilePos = new Vector3Int(chunk.bounds.x + x, chunk.bounds.y + y, 0);
                    foreach(var (layer, tilemap) in this.layers)
                    {
                        var module = tile[layer].id;
                        if(module != LevelData.Module.INVALID)
                        {
                            tilemap.SetTile(tilePos, this.generator.context.settings.modules[module]);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Generate a new random level.
        /// </summary>
        /// <param name="e"></param>
        private void onRequestNewLevel(RequestNewLevelEvent e)
        {
            if(e.settings != null)
            {
                this.initializeNewGenerator(e.settings);
            }

            this.startGenerator();
        }
    }
}
