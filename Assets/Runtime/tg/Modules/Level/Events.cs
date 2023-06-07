using System.Collections.Generic;

namespace tg.level.events
{
    using tg.events;
    using tg.level.generator;

    /// <summary>
    /// Processed by the LevelGeneratorSystem to create a new random level based on the provided settings.
    /// </summary>
    public struct RequestNewLevelEvent : IEvent
    {
        /// <summary>
        /// If this field is null, the LevelGeneratorSystem will use the previous provided settings.
        /// </summary>
        public GeneratorSettings settings;
    }


    public struct LevelGenerationStartedEvent : IEvent {}
    public struct LevelGenerationAbortedEvent : IEvent {}

    /// <summary>
    /// Event fired by LevelGenerator once the level is fully created.
    /// </summary>
    public struct NewLevelGeneratedEvent : IEvent
    {
        public LevelData    levelData;
    }
}