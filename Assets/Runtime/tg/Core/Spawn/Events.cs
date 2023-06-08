using UnityEngine;
using Unity.Entities;

namespace tg.spawn.events
{
    using tg.events;

    public struct EntitySpawnedEvent : IEvent
    {
        public Entity       entity;   
    }

    public struct GameObjectSpawnedEvent : IEvent
    {
        public GameObject   gameObject;   
    }
}