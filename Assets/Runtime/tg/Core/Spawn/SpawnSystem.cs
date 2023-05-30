using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;


namespace tg.spawn
{
	/// <summary>
	/// Spawn system will handle the playback of a all scheduled and ready spawn reqeusts command buffers.
	/// </summary>
	[UpdateInGroup(typeof(InitializationSystemGroup))]
	internal partial class SpawnSystem : SystemBase
	{
		private NativeHashMap<uint, EntityCommandBuffer>	pending;
		private NativeHashSet<uint>							ready;

		protected override void OnCreate()
		{
			this.RequireForUpdate<SpawnRequest>();

			this.pending	= new NativeHashMap<uint, EntityCommandBuffer>(1, Allocator.Persistent);
			this.ready		= new NativeHashSet<uint>(1, Allocator.Persistent);
		}

        protected override void OnDestroy()
        {
			foreach(var KVP in this.pending)
			{
				KVP.Value.Dispose();
			}

			this.pending.Dispose();

			this.ready.Dispose();
        }

		protected override void OnUpdate()
        {
			this.ready.Clear();

			foreach(var spawnRequest in SystemAPI.Query<SpawnRequest>())
			{
				if(spawnRequest.spawnTime < SystemAPI.Time.ElapsedTime)
				{
					this.ready.Add(spawnRequest.id);	
				}
			}

			foreach(var id in this.ready)
			{
				var ECB = this.pending[id];

				ECB.Playback(this.EntityManager);

				this.pending.Remove(id);
				ECB.Dispose();
			}
        }

		internal EntityCommandBuffer create(uint spawnRequestId)
		{
			var ECB = new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);
			this.pending.Add(spawnRequestId, ECB);

			return ECB;
		}
    }

	/// <summary>
	/// Processed by the SpawnSystem.
	/// </summary>
	public unsafe struct SpawnRequest : IComponentData
    {
		private static uint			nextSpawnRequestId = 0;

		/// <summary>
		/// Unique spawn request id. This id is going to be used to determine the coresponding EntityCommandBuffer when spawning the new entity.
		/// </summary>
		public uint					id			{ get; private set; }

		/// <summary>
		/// When.
		/// </summary>
		public double				spawnTime	{ get; private set; }

		/// <summary>
		/// Schedules a new spawn request for an entity at a given location. Optionally a spawn delay can be provded.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, double delay = 0)
		{
			return SpawnRequest.create(in prefab, in location, out EntityCommandBuffer ECB, delay);
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
			var spawnRequestId				= SpawnRequest.nextSpawnRequestId++;

			var world						= World.DefaultGameObjectInjectionWorld;
			var entityManager				= world.EntityManager;

			var spawnRequest				= entityManager.CreateEntity(typeof(SpawnRequest));
			{
				entityManager.SetComponentData(spawnRequest, new SpawnRequest
				{
					id						= spawnRequestId,
					spawnTime				= entityManager.World.Time.ElapsedTime + delay,
				});
			}

			var spawner						= world.GetExistingSystemManaged<SpawnSystem>();
			ECB = spawner.create(spawnRequestId);
			
			// after processing the spawn request, make sure to destroy the initial entity of this request.
			ECB.DestroyEntity(spawnRequest);

			var entity = ECB.Instantiate(prefab);
			{
				ECB.SetComponent(entity, LocalTransform.FromPosition(location));
			}

			return entity;
		}
    }
}
