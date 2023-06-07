using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    using tg.events;

    using tg.application;
    using tg.level;
    using tg.player.events;

    /// <summary>
    /// Simple player spawn system. 
    /// </summary>
    [CreateAfter(typeof(EventQueue))]
    public partial struct PlayerManager : ISystem, IEventListener<PlayerManager>
    {
        void OnCreate (ref SystemState state)
		{
            state.RequireForUpdate<LevelData>();

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
                chunk0.bounds.x + (chunk0.bounds.width / 2),
                chunk0.bounds.y + (chunk0.bounds.height / 2),
                -1.0f);

            var playerEntity = tg.spawn.request.create(appData.playerPrefab.Result, in spawnLocation, out EntityCommandBuffer ECB);
            {
                ECB.AddComponent(playerEntity, new ComponentTypeSet(
                   typeof(Player)
                ));

                ECB.SetComponent<Player>(playerEntity, new Player
                {
                    entity = playerEntity
                });
            }
        }
    }

    /// <summary>
    /// Added to spawned player entities.
    /// </summary>
    public struct Player : IComponentData
    {
        /// <summary>
        /// Reference to managed player data.
        /// </summary>
        public Entity                   entity;
    }
}

