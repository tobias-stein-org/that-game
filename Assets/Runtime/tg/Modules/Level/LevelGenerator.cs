using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace tg.level
{
    using tg.level.generator;

    /// <summary>
    /// Simple managed system that drives the level generator.
    /// </summary>
    public partial class LevelGeneratorSystem : SystemBase
    {
        private Generator                               generator;
        private IEnumerator<LevelGeneratorStep.State>   executor;

        protected override void OnStartRunning()
        {
            // look for an initially placed LevelGeneratorData component, if it exists fire-up the generator
            if(SystemAPI.ManagedAPI.TryGetSingleton<LevelGeneratorData>(out LevelGeneratorData data))
            {
                this.generator = new Generator(data.settings);
                generator.onLevelGeneratorFinished += (in LevelData levelData) => { this.Enabled = false; };

                this.executor = generator.execute();
            }
            else
            {
                this.Enabled = false;
            }
        }

        /// <summary>
        /// As long as the previously started generator isn't finsihed, update it.
        /// </summary>
        protected override void OnUpdate()
        {
            this.executor?.MoveNext();
        }

        /// <summary>
        /// Automatically dispose of any active generator. This includes also any allocated memory for LevelData.
        /// </summary>
        protected override void OnDestroy()
        {
            this.generator?.Dispose();
        }
    }

    #region Component authoring

    public class LevelGeneratorData : IComponentData
    {
        public GeneratorSettings    settings;
    }

    public class LevelGenerator : MonoBehaviour
    {
        public GeneratorSettings    settings;

        public class Baker : Baker<LevelGenerator>
        {
            public override void Bake(LevelGenerator authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, new LevelGeneratorData
                {
                    settings = authoring.settings
                });
            }
        }
    }

    #endregion
}
