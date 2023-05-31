using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace tg.level
{
    using tg.events;
    using tg.level.generator;
    using tg.level.events;

    /// <summary>
    /// Simple managed system that drives the level generator.
    /// </summary>
    public partial class LevelGeneratorSystem : SystemBase, IEventListener<LevelGeneratorSystem>
    {
        private Generator                               generator;
        private IEnumerator<LevelGeneratorStep.State>   executor;

        protected override void OnStartRunning()
        {
            EventQueue.subscribe(this);
        }

        /// <summary>
        /// As long as the previously started generator isn't finsihed, update it.
        /// </summary>
        protected override void OnUpdate()
        {
            if(!this.executor?.MoveNext() ?? false)
            {
                this.Enabled = false;
            }
        }

        /// <summary>
        /// Automatically dispose of any active generator. This includes also any allocated memory for LevelData.
        /// </summary>
        protected override void OnDestroy()
        {
            this.destroyLastLevel();

            EventQueue.unsubscribe(this);
        }

        private void destroyLastLevel()
        {
            if(SystemAPI.TryGetSingletonEntity<LevelData>(out Entity levelData)) { this.EntityManager.DestroyEntity(levelData); }

            this.generator?.Dispose();
        }

        /// <summary>
        /// Generate a new random level.
        /// </summary>
        /// <param name="e"></param>
        private void onRequestNewLevel(RequestNewLevel e)
        {
            this.destroyLastLevel();

            if(e.settings != null)
            {
                this.generator      = new Generator(e.settings);

                this.generator.onLevelGeneratorFinished += (in LevelData levelData) =>
                {
                    this.Enabled    = false;

                    // expose LevelData as singleton
                    this.EntityManager.SetComponentData(this.EntityManager.CreateSingleton<LevelData>(), levelData);

                    // let the LevelRenderer know, we want the new data drawn.
                    EventQueue.publish(new RequestRenderLevelEvent { levelData = levelData, modules = this.generator.context.settings.modules });
                };
            }

            this.executor       = this.generator.execute();
            this.Enabled        = true;
        }
    }


    public class LevelGenerator : MonoBehaviour
    {
        public GeneratorSettings settings;

        public void Start()
        {
            EventQueue.publish(new RequestNewLevel { settings = this.settings });
        }

        public void Update()
        {
            // regenerate a new random level
            if(Input.GetKeyDown(KeyCode.Space)) { EventQueue.publish(new RequestNewLevel { settings = null }); }
        }
    }
}
