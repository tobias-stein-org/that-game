using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace tg.spawn
{
    using System.Runtime.InteropServices;
    using tg.spawn.events;

    /// <summary>
    /// Spawn system will handle the playback of a all scheduled and ready spawn reqeusts command buffers.
    /// </summary>
	//[BurstCompile]
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	internal partial class SpawnSystem : SystemBase
	{
		/// <summary>
		/// Processed by the SpawnSystem.
		/// </summary>
		private struct SpawnRequest : IComponentData
		{
			/// <summary>
			/// When.
			/// </summary>
			public double				spawnTime;

			/// <summary>
			/// Placeholder id to the actual entity to be spawned.
			/// </summary>
			public Entity				entity;

			public GCHandle				prefabGO;
		}

		private struct SpawnRequestComplete : IComponentData {}


		private UnsafeHashMap<Entity, EntityCommandBuffer>	pending;

		protected override void OnCreate()
		{
			this.RequireForUpdate<SpawnRequest>();

			this.pending	= new UnsafeHashMap<Entity, EntityCommandBuffer>(1, Allocator.Persistent);
		}

        protected override void OnDestroy()
        {
			foreach(var KVP in this.pending)
			{
				var sr = this.EntityManager.GetComponentData<SpawnRequest>(KVP.Key);
				if(sr.prefabGO.IsAllocated)
				{
					sr.prefabGO.Free();
				}

				KVP.Value.Dispose();
			}

			this.pending.Dispose();
        }

		//[BurstCompile]
		private partial struct CheckSpawnJob : IJobEntity
		{
			public double time;

			[ReadOnly]
			public UnsafeHashMap<Entity, EntityCommandBuffer>	pending;

			public NativeQueue<Entity>.ParallelWriter			process;

			public void Execute(in Entity entity, in SpawnRequest spawnRequest)
			{
				if(spawnRequest.spawnTime < this.time) { this.process.Enqueue(entity); }
			}
		}

		//[BurstCompile]
		[WithAll(typeof(SpawnRequestComplete))]
		private partial struct RemoveCompletedSpawnRequestsJob : IJobEntity
		{
			public EntityCommandBuffer.ParallelWriter			ecb;

			public void Execute([ChunkIndexInQuery] int index, in Entity entity, in SpawnRequest spawnRequest)
			{
				this.ecb.DestroyEntity(index, entity);
				tg.events.EventQueue.publish(new EntitySpawnedEvent { entity = spawnRequest.entity });
			}
		}

		//[BurstCompile]
		protected override void OnUpdate()
        {
			// Check spawn timers and spawn due objects.
			using(var process = new NativeQueue<Entity>(Allocator.TempJob))
			{
				new CheckSpawnJob
				{
					time	= SystemAPI.Time.ElapsedTime,
					pending	= this.pending,
					process	= process.AsParallelWriter()
				}.ScheduleParallel(this.Dependency).Complete();


				while(process.TryDequeue(out Entity spawnRequest))
				{
					var ECB = this.pending[spawnRequest];

					var sr = this.EntityManager.GetComponentData<SpawnRequest>(spawnRequest);
					if(sr.prefabGO.IsAllocated)
					{
						var instanceGO = UnityEngine.GameObject.Instantiate((GameObject)sr.prefabGO.Target);
						sr.prefabGO.Free();

						ECB.AddComponent(sr.entity, instanceGO.transform);
					}

					ECB.Playback(this.EntityManager);
					ECB.Dispose();


					this.pending.Remove(spawnRequest);
				}
			}

			// remove completed spawn reqeusts
			using(var ecb = new EntityCommandBuffer(Allocator.TempJob))
			{
				new RemoveCompletedSpawnRequestsJob
				{
                    ecb	= ecb.AsParallelWriter()
				}.ScheduleParallel(this.Dependency).Complete();

				ecb.Playback(this.EntityManager);
			}
		}

		/// <summary>
		/// Create a new spawn request for an Entity prefab.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="ECB"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		internal Entity create(in Entity prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0)
		{
			var spawnRequest			= this.EntityManager.CreateEntity(typeof(SpawnRequest));
			ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

			
            var entity = ECB.Instantiate(prefab);
            {
				ECB.SetComponent(entity, LocalTransform.FromPosition(location));
			}

			this.EntityManager.SetComponentData(spawnRequest, new SpawnRequest
			{
				entity					= entity,
				prefabGO				= default,
				spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay
			});

			ECB.SetComponent<SpawnRequest>(spawnRequest, new SpawnRequest
			{
				entity					= entity,
				prefabGO				= default,	
				spawnTime				= float.NaN
			});

			ECB.AddComponent<SpawnRequestComplete>(spawnRequest);

			this.pending.Add(spawnRequest, ECB);

			return entity;
		}

		/// <summary>
		/// Create a new spawn request for a entity with a GameObject prefab companion.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="ECB"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		internal Entity create(GameObject prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0)
		{
			var spawnRequest			= this.EntityManager.CreateEntity(typeof(SpawnRequest));
			ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

            var entity = ECB.CreateEntity();
            {
				ECB.AddComponent<LocalTransform>(entity);
				ECB.SetComponent(entity, LocalTransform.FromPosition(location));
			}

			this.EntityManager.SetComponentData(spawnRequest, new SpawnRequest
			{
				entity					= entity,
				spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
				prefabGO				= GCHandle.Alloc(prefab)
			});

			ECB.SetComponent<SpawnRequest>(spawnRequest, new SpawnRequest
			{
				entity					= entity,
				spawnTime				= float.NaN,
				prefabGO				= GCHandle.Alloc(prefab)
			});

			ECB.AddComponent<SpawnRequestComplete>(spawnRequest);

			this.pending.Add(spawnRequest, ECB);

			return entity;
		}
    }

	public static class request
	{
		private static SpawnSystem spawner { get { return World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SpawnSystem>(); } }

		/// <summary>
		/// Schedules a new spawn request for an entity at a given location. Optionally a spawn delay can be provded.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, double delay = 0)
		{
			return create(in prefab, in location, out EntityCommandBuffer ECB, delay);
		}

		/// <summary>
		/// Schedules a new spawn request for an entity at a given location. Provides access to the used EntityCommandBuffer, which
		/// allows access to add more commands. Optionally a spawn delay can be provided.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="ECB"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0)
		{
			return spawner.create(prefab, location, out ECB, delay);
		}

		public static Entity create(GameObject prefab, in float3 location, double delay = 0)
		{
			return create(prefab, in location, out EntityCommandBuffer ECB, delay);
		}

		public static Entity create(GameObject prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0)
		{
			return spawner.create(prefab, in location, out ECB, delay);
		}
	}
}
