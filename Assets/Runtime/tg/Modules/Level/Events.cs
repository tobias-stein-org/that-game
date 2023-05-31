using System.Collections.Generic;

namespace tg.level.events
{
    using tg.events;
    using tg.level.generator;

    /// <summary>
    /// LevelRender will listen to these events and repaint the entire tilemap with the current data provided.
    /// </summary>
    public struct RequestRenderLevelEvent : IEvent
    {
        public LevelData    levelData;
        public List<Module> modules;
    }

    /// <summary>
    /// Processed by the LevelGeneratorSystem to create a new random level based on the provided settings.
    /// </summary>
    public struct RequestNewLevel : IEvent
    {
        /// <summary>
        /// If this field is null, the LevelGeneratorSystem will use the previous provided settings.
        /// </summary>
        public GeneratorSettings settings;
    }
}