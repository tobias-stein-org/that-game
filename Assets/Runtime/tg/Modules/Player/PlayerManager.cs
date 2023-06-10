using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    using tg.events;

    using tg.application;
    using tg.level;
    using tg.player.events;
    using tg.spawn.events;

    /// <summary>
    /// Simple player spawn system. 
    /// </summary>
    [CreateAfter(typeof(EventQueue))]
    [ApplicationStateFilter]
    public partial struct PlayerManager : ISystem, IEventListener<PlayerManager>, ISystemStartStop
    {
        void OnCreate (ref SystemState state)
		{
            state.RequireForUpdate(StateManager.state(this));
            state.RequireForUpdate<LevelData>();
		}

		void OnDestroy (ref SystemState state)
		{
            state.EntityManager.DestroyEntity(state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Player) } }));
		}

        void onSpawnPlayerRequestEvent(SpawnPlayerRequestEvent e)
        {
            var appData = SystemAPI.GetSingleton<ApplicationData>();
            tg.spawn.request.create(appData.playerPrefab, in e.location);
        }

        void onEntitySpawnedEvent(EntitySpawnedEvent e)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(entityManager.HasComponent<Player>(e.entity))
            {
                EventQueue.publish(new PlayerSpawnedEvent { player = entityManager.GetComponentData<Player>(e.entity) });
            }
        }

        public void OnStartRunning(ref SystemState state)
        {
            EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerManager>(state.SystemHandle));
        }

        public void OnStopRunning(ref SystemState state)
        {
            EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerManager>(state.SystemHandle));
        }
    }
}

