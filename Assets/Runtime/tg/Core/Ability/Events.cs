using Unity.Entities;
using Unity.Mathematics;

namespace tg.ability.events
{
    using tg.events;

    /// <summary>
    /// Event send by an entity player/enemy to learn an ability.
    /// </summary>
    public struct LearnAbilityEvent : IEvent
    {
        public Entity entity;
        public Entity ability;
    }

    /// <summary>
    /// Send by an entity that want to use its ability
    /// </summary>
    public struct UseAbilityEvent : IEvent
    {
        public Entity entity;
        public Entity ability;

        public float2 point;
        public float2 direction;
    }
}