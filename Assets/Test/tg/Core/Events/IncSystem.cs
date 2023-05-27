using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Burst;

namespace tg.test.events
{
	using tg.events;

	public struct IncEvent : IEvent
	{
		public int oldValue;
		public int newValue;
	}

	public partial struct IncSystem : ISystem
	{
		public void OnCreate (ref SystemState state)
		{
		}

		public void OnDestroy (ref SystemState state)
		{
		}

		public void OnUpdate (ref SystemState state)
		{
			Debug.Log("up");
			var query = SystemAPI.QueryBuilder().WithAll<CompA>().Build();
			var entities = query.ToEntityArray(Allocator.Temp);
			var comps = query.ToComponentDataArray<CompA>(Allocator.Temp);

			foreach(var c in SystemAPI.Query<RefRW<CompA>>())
			{
				c.ValueRW.foo += 1;
				EventQueue.publish(new IncEvent { oldValue = c.ValueRO.foo - 1, newValue = c.ValueRO.foo });
			}

			var job = new IncJob();

			job.ScheduleParallel();
		
		}
	}

	//[BurstCompile]
	public partial struct IncJob : IJobEntity
	{
		public void Execute(ref CompA compA)
		{
			compA.foo += 1;
			EventQueue.publish(new IncEvent { oldValue = compA.foo - 1, newValue = compA.foo });
		}
	}
}
