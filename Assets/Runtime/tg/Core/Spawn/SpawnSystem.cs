
using System.Runtime.InteropServices;

using UnityEngine;
using Unity.Entities;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

[assembly: RegisterGenericComponentType(typeof(tg.spawn.entities.SpawnSystem.PostSpawnActionData<GameObject>))]
[assembly: RegisterGenericComponentType(typeof(tg.spawn.entities.SpawnSystem.PostSpawnActionData<Entity>))]

namespace tg.spawn
{
	using tg.application;
    using tg.spawn.events;
    using static UnityEngine.EventSystems.EventTrigger;


    /// <summary>
    /// Signature for the post action callback performed on spawned GameObject instances.
    /// </summary>
    /// <param name="instance"></param>
	public delegate void PostSpawnAction<T>(T instance);

	namespace entities
	{
		/// <summary>
		/// Spawn system will handle the playback of a all scheduled and ready spawn reqeusts command buffers.
		/// </summary>
		//[BurstCompile]
		[UpdateInGroup(typeof(InitializationSystemGroup))]
		[ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
		internal partial class SpawnSystem : SystemBase
		{
			/// <summary>
			/// Processed by the SpawnSystem.
			/// </summary>
			private struct SpawnRequest : IComponentData, System.IEquatable<SpawnRequest>
			{
				public Entity				id;

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
				public int					objectId;

				public bool Equals(SpawnRequest other) { return this.id.Equals(other.id); }

				public bool isValid			{ get { return this.id != Entity.Null; } }
			}

			internal class PostSpawnActionData<T> : IComponentData
			{
				public PostSpawnAction<T>		postSpawnAction;
			}

			private struct SpawnRequestComplete : IComponentData {}


			private UnsafeHashMap<SpawnRequest, EntityCommandBuffer>	pending;

			private EntityQuery postSpawnEntity;
			private EntityQuery postSpawnObject;
			private EntityQuery removeCompleted;

			protected override void OnCreate()
			{
				this.RequireForUpdate(StateManager.state(this));
				this.RequireForUpdate<SpawnRequest>();

				this.postSpawnEntity	= SystemAPI.QueryBuilder().WithAll<SpawnRequest, SpawnRequestComplete, PostSpawnActionData<Entity>>().Build();
				this.postSpawnObject	= SystemAPI.QueryBuilder().WithAll<SpawnRequest, SpawnRequestComplete, PostSpawnActionData<GameObject>>().Build();
				this.removeCompleted	= SystemAPI.QueryBuilder().WithAll<SpawnRequest, SpawnRequestComplete>().Build();

				this.pending			= new UnsafeHashMap<SpawnRequest, EntityCommandBuffer>(256, Allocator.Persistent);
			}

			protected override void OnDestroy()
			{
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
						if(!spawnRequest.isValid) { continue; }

						var ECB = this.pending[spawnRequest];

						if(spawnRequest.entity != Entity.Null)
						{
							ECB.SetComponent(spawnRequest.entity, LocalTransform.FromPosition(spawnRequest.location));
						}

						ECB.Playback(this.EntityManager);
						ECB.Dispose();

						if(spawnRequest.objectId != -1)
						{
							// note: we reuse the initial prefab varaible and replace it with the actual object instance
							spawnRequest.objectId = UnityEngine.GameObject.Instantiate(
								Resources.InstanceIDToObject(spawnRequest.objectId),
								new Vector3(spawnRequest.location.x, spawnRequest.location.y, spawnRequest.location.z),
								Quaternion.identity).GetInstanceID();

							// update request data
							this.EntityManager.SetComponentData<SpawnRequest>(spawnRequest.id, spawnRequest);
						}

						this.pending.Remove(spawnRequest);
					}

					// process all entity post spawn actions
					if(!this.postSpawnEntity.IsEmpty)
					{
						var req  = this.postSpawnEntity.ToComponentDataArray<SpawnRequest>(Allocator.Temp);
						var post = this.postSpawnEntity.ToComponentArray<PostSpawnActionData<Entity>>();

						for(int i = 0; i < req.Length; i++) { post[i].postSpawnAction(req[i].entity); }
					}

					if(!this.postSpawnObject.IsEmpty)
					{
						var req  = this.postSpawnObject.ToComponentDataArray<SpawnRequest>(Allocator.Temp);
						var post = this.postSpawnObject.ToComponentArray<PostSpawnActionData<GameObject>>();

						for(int i = 0; i < req.Length; i++) { post[i].postSpawnAction(Resources.InstanceIDToObject(req[i].objectId) as GameObject); }
					}

					if(!this.removeCompleted.IsEmpty)
					{
						var req  = this.removeCompleted.ToComponentDataArray<SpawnRequest>(Allocator.Temp);

						for(int i = 0; i < req.Length; i++)
						{
							if(req[i].objectId != -1)
							{
								tg.events.EventQueue.publish(new GameObjectSpawnedEvent { gameObject = Resources.InstanceIDToObject(req[i].objectId) as GameObject });
							}

							if(req[i].entity != Entity.Null)
							{
								tg.events.EventQueue.publish(new EntitySpawnedEvent { entity = req[i].entity });
                            }

							this.EntityManager.DestroyEntity(req[i].id);
						}
					}
				}
			}

			internal Entity create(in float3 location, out EntityCommandBuffer ECB, double delay = 0, PostSpawnAction<Entity> postSpawnAction = null)
			{
				ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

				var entity					= ECB.CreateEntity();
				{
					ECB.AddComponent<Unity.Transforms.LocalTransform>(entity);
				}

				var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

				var spawnRequest			= new SpawnRequest
				{
					id						= spawnRequestEntity,
					entity					= entity,
					objectId				= -1,
					spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
					location				= location
				};

				this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);

				if(postSpawnAction != null)
				{
					this.EntityManager.AddComponentObject(spawnRequestEntity, new PostSpawnActionData<Entity> { postSpawnAction = postSpawnAction });
				}

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
			internal Entity create(in Entity prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0, PostSpawnAction<Entity> postSpawnAction = null)
			{
				ECB							= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

				var entity					= ECB.Instantiate(prefab);
				var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

				var spawnRequest			= new SpawnRequest
				{
					id						= spawnRequestEntity,
					entity					= entity,
					objectId				= -1,
					spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
					location				= location
				};

				this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);

				if(postSpawnAction != null)
				{
					this.EntityManager.AddComponentObject(spawnRequestEntity, new PostSpawnActionData<Entity> { postSpawnAction = postSpawnAction });
				}

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
			internal void create(GameObject prefab, in float3 location, double delay = 0, PostSpawnAction<GameObject> postSpawnAction = null)
			{
				var ECB						= new EntityCommandBuffer(Allocator.Persistent, PlaybackPolicy.SinglePlayback);

				var spawnRequestEntity		= this.EntityManager.CreateEntity(typeof(SpawnRequest));

				
				var spawnRequest			= new SpawnRequest
				{
					id						= spawnRequestEntity,
					entity					= Entity.Null,
					objectId				= prefab.GetInstanceID(),
					spawnTime				= this.EntityManager.World.Time.ElapsedTime + delay,
					location				= location
				};

				this.EntityManager.SetComponentData(spawnRequestEntity, spawnRequest);
				if(postSpawnAction != null)
				{
					this.EntityManager.AddComponentObject(spawnRequestEntity, new PostSpawnActionData<GameObject> { postSpawnAction = postSpawnAction });
				}

				// once the request is processed ...
				{
					// mark it completed
					ECB.AddComponent<SpawnRequestComplete>(spawnRequestEntity);
				}

				this.pending.Add(spawnRequest, ECB);
			}
		}
    }

	public static class request
	{
		private static entities.SpawnSystem spawner { get { return World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<entities.SpawnSystem>(); } }

		/// <summary>
		/// Spawns a new entity. Also provides access to the new enities ECB for further changes.
		/// </summary>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(out EntityCommandBuffer ECB, in float3 location = default, double delay = 0, PostSpawnAction<Entity> postSpawnAction = null) { return spawner.create(in location, out ECB, delay, postSpawnAction); }

		/// <summary>
		/// Spawns a new entity from a prefab. Optionally a spawn delay can be provded.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, double delay = 0, PostSpawnAction<Entity> postSpawnAction = null) { return create(in prefab, in location, out EntityCommandBuffer ECB, delay, postSpawnAction); }

		/// <summary>
		/// Spawn a new entity from a prefab. Provides access to the used EntityCommandBuffer, which
		/// allows access to add more commands. Optionally a spawn delay can be provided.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="ECB"></param>
		/// <param name="delay"></param>
		/// <returns></returns>
		public static Entity create(in Entity prefab, in float3 location, out EntityCommandBuffer ECB, double delay = 0, PostSpawnAction<Entity> postSpawnAction = null) { return spawner.create(prefab, in location, out ECB, delay, postSpawnAction); }

		/// <summary>
		/// Spawn GameObject from a prefab.
		/// </summary>
		/// <param name="prefab"></param>
		/// <param name="location"></param>
		/// <param name="delay"></param>
		public static void create(GameObject prefab, in float3 location, double delay = 0, PostSpawnAction<GameObject> postSpawnAction = null) { spawner.create(prefab, in location, delay, postSpawnAction); }
	}
}
