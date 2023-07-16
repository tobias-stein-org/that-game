using Unity.Entities;
using Unity.Mathematics;

namespace tg.game
{
	using tg.events;
	using tg.enemy;
	using tg.level;

    using tg.level.events;
	using tg.player.events;
	using tg.enemy.events;
	using tg.game.events;
	using tg.ability.events;
	using tg.ui.events;

	namespace entities
	{
        using tg.application.entities;

        /// <summary>
        /// Start new Game does the following things:
        /// 1. Requests a new level
        /// 2. Spawns enemies
        /// 3. Spawns the player
        /// </summary>
		[CreateAfter(typeof(EventQueue))]
		public partial class StartNew : SystemBase, IEventListener<StartNew>
		{
            protected override void OnStartRunning()
			{
                EventQueue.subscribe(this);
			}


            protected override void OnStopRunning()
            {
				EventQueue.unsubscribe(this);
            }

            protected override void OnUpdate()
			{
			}

			void onStartNewGameEvent(StartGameEvent e)
			{
				ui.transition.showLoadingScreen();

                EventQueue.publish(new UpdateLoadingScreenProgressEvent { progress = 0f/3f, step = "Generate new level..." });

                EventQueue.publish(new RequestNewLevelEvent {});
			}


            void onNewLevelGeneratedEvent(NewLevelGeneratedEvent e)
			{
                EventQueue.publish(new UpdateLoadingScreenProgressEvent { progress = 1f/3f, step = "Spawn the bad guys..." });

                StartNew.spawnEnemies(in e.levelData);
			}

			private static void spawnEnemies(in LevelData levelData)
			{
				var defaultBehaviour = World.DefaultGameObjectInjectionWorld.EntityManager.GetComponentObject<ApplicationData>(World.DefaultGameObjectInjectionWorld.GetExistingSystem<Applicaiton>()).defaultEnemyBehaviour;

				for(int i = 1; i < levelData.numChunks; i++)
				{
					var rng = Random.CreateFromIndex((uint)i);

					ref var chunk = ref levelData.getChunkById(i);

					int requested = 0;
					while(requested < 4)
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

				//EventQueue.publish(new SpawnEnemyRequestEvent
				//{
				//	amount = 1,
				//	desc = new SpawnRequestDescription
				//	{
				//		location = new float3(10.5f, 10.0f, 0.0f),
				//		behaviour = defaultBehaviour
				//	}
				//});

				StartNew.spawnPlayer(in levelData);
			}

			private static void spawnPlayer(in LevelData levelData)
			{
                EventQueue.publish(new UpdateLoadingScreenProgressEvent { progress = 2f / 3f, step = "Spawn the good guy..." });

                var chunk0 = levelData.getChunk(0);

				var spawnLocation = new float3(
					chunk0.bounds.x + (chunk0.bounds.width  / 2),
					chunk0.bounds.y + (chunk0.bounds.height / 2),
					-1.0f);

				EventQueue.publish(new SpawnPlayerRequestEvent { location = spawnLocation });
			}

			void onPlayerSpawnedEvent(PlayerSpawnedEvent e)
			{
				EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = e.player, ability = "MELEE_ATT" });
				EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = e.player, ability = "FIREBALL_1" });
				EventQueue.publish(new LearnAbilityEvent<Unity.Collections.FixedString64Bytes> { entity = e.player, ability = "ICEBLAST_1" });

                EventQueue.publish(new UpdateLoadingScreenProgressEvent { progress = 3f / 3f, step = "Good luck." });

                EventQueue.publish(new NewGameStartedEvent {});
				//ui.transition.hideLoadingScreen();
            }
        }
	}
}
