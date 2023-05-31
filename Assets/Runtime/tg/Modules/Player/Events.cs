using Unity.Entities;

namespace tg.player.events
{
    using tg.events;

    public struct PlayerSpawnedEvent : IEvent
    {
        public Entity   player;
    }

    public struct PlayerDiedEvent : IEvent
    {
        public Entity   player;
    }
}