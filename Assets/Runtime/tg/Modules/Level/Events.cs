using System.Collections.Generic;

namespace tg.level.events
{
    using tg.events;

    /// <summary>
    /// LevelRender will listen to these events and repaint the entire tilemap with the current data provided.
    /// </summary>
    public struct RequestRenderLevelEvent : IEvent
    {
        public LevelData    levelData;
        public List<Module> modules;
    }
}