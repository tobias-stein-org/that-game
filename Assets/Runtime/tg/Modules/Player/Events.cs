using Unity.Entities;
using Unity.Mathematics;

namespace tg.player.events
{
    using tg.events;

    public struct SpawnPlayerRequestEvent : IEvent
    {
        public float3   location;
    }

    public struct PlayerSpawnedEvent : IEvent
    {
        public Player   player;
    }


    public struct KillPlayerEvent : IEvent
    {
        public Player   player;
    }

    public struct PlayerDiedEvent : IEvent
    {
        public Player   player;
    }

    /// <summary>
    /// Fired by the PlayerTracker system, when player walked from on level chunk into another.
    /// </summary>
    public struct PlayerLevelChunkChangeEvent : IEvent
    {
        public Player   player;

        /// <summary>
        /// Level chunk id the player entered.
        /// </summary>
        public int      enter;

        /// <summary>
        /// Level chunk id the player left.
        /// </summary>
        public int      exit;
    }
}