
using Unity.Entities;

namespace tg.combat.events
{
    using tg.events;
    using tg.combat;

    public struct DealDamageEvent : IEvent
    {
        public Entity source;
        public Entity target;

        public Damage damage;
    }

    public struct EntityDiedEvent : IEvent
    {
        public Entity entity;
    }
}
