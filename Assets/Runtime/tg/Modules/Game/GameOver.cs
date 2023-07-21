using Unity.Entities;

namespace tg.game
{
	using tg.events;
	using tg.level;
	using tg.player.events;
	using tg.level.events;
	using tg.game.events;
	using tg.combat.events;

	namespace entities
	{
        using tg.application.entities;

        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenLoading, false)]
		[CreateAfter(typeof(EventQueue))]
		public partial struct GameOver : ISystem, IEventListener<GameOver>, ISystemStartStop
		{
			private int lastChunk;

			void OnCreate (ref SystemState state)
			{
				state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<GameOver>(state.SystemHandle)));
				lastChunk = LevelData.Chunk.INVALID.id;
			}

			void OnDestroy (ref SystemState state)
			{
			}

			void OnUpdate (ref SystemState state)
			{
			}

			void onNewLevelGeneratedEvent(NewLevelGeneratedEvent e)
			{
				ref var self = ref World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<GameOver>(World.DefaultGameObjectInjectionWorld.GetExistingSystem<GameOver>());

				self.lastChunk = e.levelData.getChunk(e.levelData.numChunks - 1).id;
			}

			void onPlayerLevelChunkChangeEvent(PlayerLevelChunkChangeEvent e)
			{
				var self = World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<GameOver>(World.DefaultGameObjectInjectionWorld.GetExistingSystem<GameOver>());

				if(e.enter == self.lastChunk)
				{
					EventQueue.publish(new EntityDiedEvent { entity = e.player });
					EventQueue.publish(new GameOverEvent { reason = GameOverEvent.Reason.PLAYER_DONE });

					tg.data.access.GAME_GAMEOVER.upsert(GameOverEvent.Reason.PLAYER_DONE);
                }
			}

			void onEntityDiedEvent(combat.events.EntityDiedEvent e)
			{
				if(!tg.data.access.GAME_GAMEOVER.isValid)
				{
					if(World.DefaultGameObjectInjectionWorld.EntityManager.HasComponent<tg.player.entities.Player>(e.entity))
					{
						EventQueue.publish(new GameOverEvent { reason = GameOverEvent.Reason.PLAYER_LOST });

						tg.data.access.GAME_GAMEOVER.upsert(GameOverEvent.Reason.PLAYER_LOST);
					}
				}
            }

			public void OnStartRunning(ref SystemState state)
			{
				EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<GameOver>(state.SystemHandle));
			}

			public void OnStopRunning(ref SystemState state)
			{
				EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<GameOver>(state.SystemHandle));
			}
		}
	}
}
