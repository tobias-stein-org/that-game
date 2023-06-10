using Unity.Entities;

namespace tg.game
{
	using tg.application;
	using tg.events;
    using tg.level.events;
	using tg.player.events;
	using tg.game.events;

	[ApplicationStateFilter(ApplicationStateMask.AllowRunWhenGameOver, false)]
	[CreateAfter(typeof(EventQueue))]
    public partial struct StartNew : ISystem, ISystemStartStop, IEventListener<StartNew>
    {
        void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate(StateManager.state(this));
		}

		void OnDestroy(ref SystemState state)
		{
		}

		void OnUpdate(ref SystemState state)
		{
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
			var chunk0 = e.levelData.getChunk(0);

            var spawnLocation = new Unity.Mathematics.float3(
                chunk0.bounds.x + (chunk0.bounds.width  / 2),
                chunk0.bounds.y + (chunk0.bounds.height / 2),
                -1.0f);

			EventQueue.publish(new SpawnPlayerRequestEvent { location = spawnLocation });
			//EventQueue.unsubscribe(World.DefaultGameObjectInjectionWorld.Unmanaged.GetUnsafeSystemRef<StartNew>(World.DefaultGameObjectInjectionWorld.Unmanaged.GetExistingUnmanagedSystem<StartNew>()));
		}
    }
}
