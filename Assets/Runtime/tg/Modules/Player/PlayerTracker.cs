using Unity.Entities;
using Unity.Transforms;
using Unity.Physics;
using Unity.Burst;
using Unity.Collections;

namespace tg.player
{
	using tg.events;
	using tg.level;
	using tg.player.events;
	using tg.level.events;

    [UpdateAfter(typeof(TransformSystemGroup))]
    public partial struct PlayerTracker : ISystem, IEventListener<PlayerTracker>, ISystemStartStop
    {
		void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
			state.RequireForUpdate<LevelData>();
		}

		public void OnStartRunning(ref SystemState state)
		{
			
			EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerTracker>(state.SystemHandle));
		}

		public void OnStopRunning(ref SystemState state)
		{
			EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerTracker>(state.SystemHandle));
		}

		void OnDestroy(ref SystemState state)
		{
		}

		[BurstCompile]
		public void OnUpdate(ref SystemState state)
		{
		
		}

    }

}
