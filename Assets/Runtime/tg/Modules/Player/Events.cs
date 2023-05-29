using Unity.Entities;

namespace tg.player.events
{
    using tg.events;

    public struct PlayerSpawned : IEvent
    {
        public Entity   player;
    }

    public struct PlayerDied : IEvent
    {
        public Entity   player;
    }
}