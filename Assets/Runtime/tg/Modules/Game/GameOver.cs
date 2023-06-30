using Unity.Entities;
using UnityEngine.Scripting;

namespace tg.game
{
	using tg.application.entities;
	using tg.events;
	using tg.level;
	using tg.player.events;
	using tg.level.events;
	using tg.game.events;

	[ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
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
				EventQueue.publish(new KillPlayerEvent { player = e.player });
				EventQueue.publish(new GameOverEvent {});
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
