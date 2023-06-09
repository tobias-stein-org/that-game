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
    public partial struct PlayerManager : ISystem, IEventListener<PlayerManager>
    {
        void OnCreate (ref SystemState state)
		{
            state.RequireForUpdate<LevelData>();
            state.RequireForUpdate<ApplicationDataLoaded>();

            EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerManager>(state.SystemHandle));
		}

		void OnDestroy (ref SystemState state)
		{
            state.EntityManager.DestroyEntity(state.GetEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Player) } }));
		}

        void onSpawnPlayerRequestEvent(SpawnPlayerRequestEvent e)
        {
            var appData = SystemAPI.GetSingleton<ApplicationData>();

            var chunk0 = SystemAPI.GetSingleton<LevelData>().getChunk(0);

            var spawnLocation = new Unity.Mathematics.float3(
                chunk0.bounds.x + (chunk0.bounds.width  / 2),
                chunk0.bounds.y + (chunk0.bounds.height / 2),
                -1.0f);

            tg.spawn.request.create(appData.playerPrefab, in spawnLocation);
        }

        void onEntitySpawnedEvent(EntitySpawnedEvent e)
        {
            var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            if(entityManager.HasComponent<Player>(e.entity))
            {
                EventQueue.publish(new PlayerSpawnedEvent { player = entityManager.GetComponentData<Player>(e.entity) });
            }
        }
    }
}

