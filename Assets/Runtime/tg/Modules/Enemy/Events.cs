using Unity.Entities;

namespace tg.enemy.events
{
    using tg.events;

    public struct SpawnEnemyRequestEvent : IEvent
    {
        public int                      amount;
        public SpawnRequestDescription  desc;
    }

    public struct EnemySpawnedEvent : IEvent
    {
        public Entity enemy;
    }

    public struct KillEnemyEvent : IEvent
    {
        public Entity enemy;
    }

    public struct EnemyDiedEvent : IEvent
    {
        public Entity enemy;
    }
}