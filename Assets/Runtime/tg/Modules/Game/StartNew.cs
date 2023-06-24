using Unity.Entities;
using Unity.Mathematics;

namespace tg.game
{
	using tg.application;
	using tg.events;
	using tg.enemy;
	using tg.level;
	using tg.ability;
    using tg.ability.entities;

    using tg.level.events;
	using tg.player.events;
	using tg.enemy.events;
	using tg.game.events;
	using tg.ability.events;

    /// <summary>
    /// Start new Game does the following things:
    /// 1. Requests a new level
    /// 2. Spawns enemies
    /// 3. Spawns the player
    /// </summary>
	[ApplicationStateFilter(ApplicationStateMask.AllowRunWhenGameOver, false)]
	[CreateAfter(typeof(EventQueue))]
    public partial struct StartNew : ISystem, ISystemStartStop, IEventListener<StartNew>
    {
        void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate(StateManager.state(this));
		}


		public void OnStartRunning(ref SystemState state)
        {
			EventQueue.subscribe(this);

			EventQueue.publish(new NewGameStartedEvent {});
			EventQueue.publish(new RequestNewLevelEvent {});
        }

        public void OnStopRunning(ref SystemState state)
        {
        }


		void onNewLevelGeneratedEvent(NewLevelGeneratedEvent e)
		{
			StartNew.spawnEnemies(in e.levelData);
		}

		private static void spawnEnemies(in LevelData levelData)
		{
			var defaultBehaviour = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(ApplicationData)).GetSingleton<ApplicationData>().defaultEnemyBehaviour;

			for(int i = 1; i < levelData.numChunks; i++)
			{
				var rng = Random.CreateFromIndex((uint)i);

				ref var chunk = ref levelData.getChunkById(i);

				int requested = 0;
				while(requested < 3)
				{
					var tileId = rng.NextInt(chunk.data.Length);
					var tile = chunk[tileId];
					if(tile.constructionType != LevelData.Tile.ConstructionType.Walkable) { continue; }

					var tilePosX = tileId % chunk.bounds.width;
					var tilePoxY = tileId / chunk.bounds.width;

					EventQueue.publish(new SpawnEnemyRequestEvent
					{
						amount = 1,
						desc = new SpawnRequestDescription
						{
							location = new float3(chunk.bounds.position.x + tilePosX, chunk.bounds.position.y + tilePoxY, 0.0f),
							behaviour = defaultBehaviour
						}
					});

					requested++;
				}
			}

			EventQueue.publish(new SpawnEnemyRequestEvent
			{
				amount = 1,
				desc = new SpawnRequestDescription
				{
					location = new float3(10.5f, 10.0f, 0.0f),
					behaviour = defaultBehaviour
				}
			});

			StartNew.spawnPlayer(in levelData);
        }

		private static void spawnPlayer(in LevelData levelData)
		{
			var chunk0 = levelData.getChunk(0);

            var spawnLocation = new float3(
                chunk0.bounds.x + (chunk0.bounds.width  / 2),
                chunk0.bounds.y + (chunk0.bounds.height / 2),
                -1.0f);

			EventQueue.publish(new SpawnPlayerRequestEvent { location = spawnLocation });
		}

		void onPlayerSpawnedEvent(PlayerSpawnedEvent e)
		{
			//var abilities = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(AbilityData)).ToEntityArray(Unity.Collections.Allocator.Temp);
			//if(abilities.Length > 0)
			//{
			//	EventQueue.publish(new LearnAbilityEvent<Entity> { entity = e.player.entity, ability = abilities[0] });
			//}

			//abilities.Dispose();

			EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = e.player.entity, ability = "FIREBALL_1" });

			EventQueue.unsubscribe(World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<StartNew>(World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingUnmanagedSystem<StartNew>()));
		}
    }
}
