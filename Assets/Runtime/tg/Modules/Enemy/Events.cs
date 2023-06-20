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
        public Enemy enemy;
    }

    public struct KillEnemyEvent : IEvent
    {
        public Enemy enemy;
    }

    public struct EnemyDiedEvent : IEvent
    {
        public Enemy enemy;
    }
}