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
    using static UnityEditor.FilePathAttribute;
    using static UnityEngine.EventSystems.EventTrigger;

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
		private struct SpawnRequest : IComponentData, System.IEquatable<SpawnRequest>
		{
			public int					id;

			/// <summary>
			/// When.
			/// </summary>
			public double				spawnTime;

			/// <summary>
			/// Where.
			/// </summary>
			public float3				location;

			/// <summary>
			/// Placeholder id to the actual entity to be spawned.
			/// </summary>
			public Entity				entity;

			/// <summary>
			/// If a GameObject is created instead of an entity, this GCHandle will hold a valid reference to the prefab.
			/// </summary>
			public GCHandle				prefabGO;

            public bool Equals(SpawnRequest other) { return this.id.Equals(other.id); }
        }

		private struct SpawnRequestComplete : IComponentData {}


		private UnsafeHashMap<SpawnRequest, EntityCommandBuffer>	pending;

		private int													nextRequestId;

		protected override void OnCreate()
		{
			this.RequireForUpdate<SpawnRequest>();

			this.pending		= new UnsafeHashMap<SpawnRequest, EntityCommandBuffer>(1, Allocator.Persistent);
			this.nextRequestId	= 0;
		}

        protected override void OnDestroy()
        {
			foreach(var KVP in this.pending)
			{
				if(KVP.Key.prefabGO.IsAllocated)
				{
					KVP.Key.prefabGO.Free();
				}

				//if(KVP.Value.IsCreated) { KVP.Value.Dispose(); }
			}

			this.pending.Dispose();
        }

		//[BurstCompile]
		private partial struct CheckSpawnJob : IJobEntity
		{
			public double time;

			[ReadOnly]
			public UnsafeHashMap<SpawnRequest, EntityCommandBuffer>	pending;

			public NativeQueue<SpawnRequest>.ParallelWriter			process;

			public void Execute(in SpawnRequest spawnRequest)
			{
				if(spawnRequest.spawnTime < this.time) { this.process.Enqueue(spawnRequest); }
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

				if(spawnRequest.entity != Entity.Null)
				{
					tg.events.EventQueue.publish(new EntitySpawnedEvent { entity = spawnRequest.entity });
				}
			}
		}

		//[BurstCompile]
		protected override void OnUpdate()
        {
			// Check spawn timers and spawn due objects.
			using(var process = new NativeQueue<SpawnRequest>(Allocator.TempJob))
			{
				new CheckSpawnJob
				{
					time	= SystemAPI.Time.ElapsedTime,
					pending	= this.pending,
					process	= process.AsParallelWriter()
				}.ScheduleParallel(this.Dependency).Complete();


				while(process.TryDequeue(out SpawnRequest spawnRequest))
				{
					var ECB = this.pending[spawnRequest];

					if(spawnRequest.prefabGO.IsAllocated)
					{
						var instanceGO = UnityEngine.GameObject.Instantiate(
							(GameObject)spawnRequest.prefabGO.Target,
							new Vector3(spawnRequest.location.x, spawnRequest.location.y, spawnRequest.location.z),
							Quaternion.identity);

                        spawnRequest.prefabGO.Free();

						tg.events.EventQueue.publish(new GameObjectSpawnedEvent { gameObject = instanceGO });
                    }

					if(spawnRequest.entity != Entity.Null)
                    {
						ECB.SetComponent(spawnRequest.entity, LocalTransform.FromPosition(spawnRequest.location));
					}

					ECB.Playback(this.EntityManager);
					ECB.Dispose();

					this.pending.Remove(spawnRequest);
				}
			}

			// remove completed spawn reqeusts
			using(var ecb = new EntityCommandBuffer(Allocator.TempJob))
			{
				new RemoveCompletedSpawnRequestsJob { ecb = ecb.AsParallelWriter() }.ScheduleParallel(this.Dependency).Complete();
				ecb.Playback(this.EntityManager);
			}
		}


		internal Entity create(in float3 location, out EntityCommandBuffer ECB, double delay = 0)
		{
			ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

            var entity					= ECB.CreateEntity();
			{
				ECB.AddComponent<Unity.Transforms.LocalTransform>(entity);
			}

			var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

			var spawnRequest			= new SpawnRequest
			{
				id						= this.nextRequestId++,
				entity					= entity,
				prefabGO				= default,
				spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
				location				= location
			};

			this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);

			// once the request is processed ...
			{
				// this will ensure the placeholder entity field gets updated
				ECB.SetComponent<SpawnRequest>(spawnRequestEntity, spawnRequest);

				// mark it completed
				ECB.AddComponent<SpawnRequestComplete>(spawnRequestEntity);
			}

			this.pending.Add(spawnRequest, ECB);

			return entity;
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
			ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

            var entity					= ECB.Instantiate(prefab);
			var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

			var spawnRequest			= new SpawnRequest
			{
				id						= this.nextRequestId++,
				entity					= entity,
				prefabGO				= default,
				spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
				location				= location
			};

			this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);

			// once the request is processed ...
			{
				// this will ensure the placeholder entity field gets updated
				ECB.SetComponent<SpawnRequest>(spawnRequestEntity, spawnRequest);
				// mark it completed
				ECB.AddComponent<SpawnRequestComplete>(spawnRequestEntity);
			}

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
		internal void create(GameObject prefab, in float3 location, double delay = 0)
		{
			var ECB						= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

			var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

			var spawnRequest			= new SpawnRequest
			{
				id						= this.nextRequestId++,
				entity					= Entity.Null,
				prefabGO				= GCHandle.Alloc(prefab),
				spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
				location				= location
			};

			this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);

			// once the request is processed ...
			{
				// mark it completed
				ECB.AddComponent<SpawnRequestComplete>(spawnRequestEntity);
			}

			this.pending.Add(spawnRequest, ECB);
		}
    }

	public static class request
	{
		private static SpawnSystem spawner { get { return World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<SpawnSystem>(); } }

		/// <summary>
		/// Spawns a new entity. Also provides access to the new enities ECB for further changes.
		/// </summary>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(out EntityCommandBuffer ECB, in float3 location = default, double delay = 0) { return spawner.create(in location, out ECB, delay); }


		/// <summary>
		/// Spawns a new entity from a prefab. Optionally a spawn delay can be provded.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, double delay = 0) { return create(in prefab, in location, out EntityCommandBuffer ECB, delay); }

		/// <summary>
		/// Spawn a new entity from a prefab. Provides access to the used EntityCommandBuffer, which
		/// allows access to add more commands. Optionally a spawn delay can be provided.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="ECB"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0) { return spawner.create(prefab, in location, out ECB, delay); }

		/// <summary>
		/// Spawn GameObject from a prefab.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		public static void create(GameObject prefab, in float3 location, double delay = 0) { spawner.create(prefab, in location, delay); }
	}
}
