using System;

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace tg.player
{
    using tg.application.entities;
	using tg.events;
	using tg.level;
	using tg.player.events;
	using tg.level.events;
    using tg.spawn.events;

	using RectInt = UnityEngine.RectInt;


    namespace entities
    {
        /// <summary>
        /// This system keeps track in which level chunk the player is currently located.
        /// </summary>
        [UpdateAfter(typeof(TransformSystemGroup))]
        [CreateAfter(typeof(EventQueue))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct PlayerTracker : ISystem, IEventListener<PlayerTracker>, ISystemStartStop
        {
            /// <summary>
            /// Utility spartial quadtree struct to accelerate queries.
            /// </summary>
		    private struct LevelChunkQuadtree : IDisposable, IComponentData
            {
                internal struct Node : IDisposable
                {
                    public int4                         bounds;
                    public int4                         childs;
                    public UnsafeList<LevelData.Chunk>  chunks;

                    public Node(in RectInt bounds)
                    {
                        this.bounds         = new int4(bounds.xMin, bounds.yMin, bounds.xMax, bounds.yMax);
                        this.childs         = -1;
                        this.chunks         = new UnsafeList<LevelData.Chunk>(1, Allocator.Persistent);
                    }

                    public bool contains(in RectInt bounds)
                    {
                        return (this.bounds.x <= bounds.xMin && this.bounds.y <= bounds.yMin && this.bounds.z >= bounds.xMax && this.bounds.w >= bounds.yMax);
                    }

                    public bool contains(in float3 position)
                    {
                        return (this.bounds.x <= position.x && this.bounds.y <= position.y && this.bounds.z >= position.x && this.bounds.w >= position.y);
                    }

                    public void Dispose()
                    {
                        if(this.chunks.IsCreated) { this.chunks.Dispose(); }
                    }
                }

                private NativeList<Node>    nodes;

                public LevelChunkQuadtree(in LevelData level)
                {
                    // figure out level bounds
                    var levelBounds         = level.numChunks > 0 ? level.chunks[0].bounds : default;
                    for(int i = 1; i < level.numChunks; i++)
                    {
                        ref var chunk       = ref level.getChunk(i);
               
                        levelBounds.xMin    = math.min(levelBounds.xMin, chunk.bounds.xMin);
                        levelBounds.yMin    = math.min(levelBounds.yMin, chunk.bounds.yMin);
                        levelBounds.xMax    = math.max(levelBounds.xMax, chunk.bounds.xMax);
                        levelBounds.yMax    = math.max(levelBounds.yMax, chunk.bounds.yMax);
                    }

                    // make it a quad
                    levelBounds.width       = math.max(levelBounds.width, levelBounds.height);
                    levelBounds.height      = math.max(levelBounds.width, levelBounds.height);

                    this.nodes              = new NativeList<Node>(level.numChunks, Allocator.Persistent);

                    this.nodes.Add(new Node(levelBounds));

                    // add all level chunks to quadtree
                    for(int i = 0; i < level.numChunks; i++) { this.insert(0, in level.getChunk(i)); }
                }

                private void insert(int nodeIndex, in LevelData.Chunk chunk)
                {
                    var node = this.nodes[nodeIndex];

                    if(node.contains(in chunk.bounds))
                    {
                        // create child nodes if they don't exist yet
                        if (node.childs.x == -1)
                        {
                            node = this.createChildNodes(nodeIndex);
                        }

                        // Insert the object into the appropriate child node(s)
                        for (int i = 0; i < 4; i++)
                        {
                            var child = this.nodes[node.childs[i]];
                            if(child.contains(in chunk.bounds))
                            {
                                this.insert(node.childs[i], in chunk);
                                return;
                            }
                        }
                    }

                    node.chunks.Add(chunk);
                    this.nodes[nodeIndex] = node;
                }

                public LevelData.Chunk query(in float3 position) { return this.query(0, in position); }
                public LevelData.Chunk query(in UnityEngine.Vector3 position) { return this.query(0, new float3(position.x, position.y, position.z)); }

                private LevelData.Chunk query(int nodeIndex, in float3 position)
                {
                    var node = this.nodes[nodeIndex];

                    // Check if the node's bounds intersect with the query region
                    if (node.contains(position))
                    {
                        foreach(var chunk in node.chunks)
                        {
                            if(chunk.bounds.Contains(new UnityEngine.Vector2Int(UnityEngine.Mathf.FloorToInt(position.x), UnityEngine.Mathf.FloorToInt(position.y))))
                            {
                                return chunk;
                            }
                        }

                        // Recursively query child nodes if they exist
                        if (node.childs.x != -1)
                        {
                            for (int i = 0; i < 4; i++)
                            {
                                var chunk = this.query(node.childs[i], in position);
                                if(chunk.valid)
                                {
                                    return chunk;
                                }
                            }
                        }
                    }

                    return LevelData.Chunk.INVALID;
                }

                private Node createChildNodes(int parentIndex)
                {
                    var parentNode      = this.nodes[parentIndex];
                    var parentBounds    = parentNode.bounds;

                    // Calculate the bounds for each child node
                    int childWidth      = (parentBounds.z - parentBounds.x) / 2;
                    int childHeight     = (parentBounds.w - parentBounds.y) / 2;

                    RectInt[] childBounds = new RectInt[]
                    {
                        new RectInt(parentBounds.x,                 parentBounds.y,                 childWidth, childHeight),
                        new RectInt(parentBounds.x + childWidth,    parentBounds.y,                 childWidth, childHeight),
                        new RectInt(parentBounds.x,                 parentBounds.y + childHeight,   childWidth, childHeight),
                        new RectInt(parentBounds.x + childWidth,    parentBounds.y + childHeight,   childWidth, childHeight)
                    };

                    // Create the child nodes and update the parent's child indices
                    for (int i = 0; i < 4; i++)
                    {
                        parentNode.childs[i] = this.nodes.Length;
                        this.nodes.Add(new Node(childBounds[i]));
                    }

                    return (this.nodes[parentIndex] = parentNode);
                }

                public void Dispose()
                {
                    if(this.nodes.IsCreated)
                    {
                        foreach(var node in this.nodes) { node.Dispose(); }
                        this.nodes.Dispose();
                    }
                }

                public override string ToString()
                {
                    string buffer = "";

                    foreach(var node in this.nodes)
                    {
                        buffer += $"{node.bounds}";
                        foreach(var c in node.chunks)
                        {
                            buffer += $"{c},";
                        }
                        buffer += "\n";
                    }

                    return buffer;
                }
            }

		    void OnCreate(ref SystemState state)
		    {
                state.RequireForUpdate(StateManager.state(this));
                state.RequireForUpdate<Player>();
                state.RequireForUpdate<LevelChunkQuadtree>();

			    EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerTracker>(state.SystemHandle));
            }

		    public void OnStartRunning(ref SystemState state)
		    {
		    }

		    public void OnStopRunning(ref SystemState state)
		    {
			    EventQueue.unsubscribe(state.WorldUnmanaged.GetUnsafeSystemRef<PlayerTracker>(state.SystemHandle));
		    }

		    void OnDestroy(ref SystemState state)
		    {
                this.destroyOldQuadtreeData();
		    }

		    public void OnUpdate(ref SystemState state)
		    {
                var quadtree = SystemAPI.GetSingleton<LevelChunkQuadtree>();

                foreach(var (player, transform, playerLevelChunk, entity) in SystemAPI.Query<Player, SystemAPI.ManagedAPI.UnityEngineComponent<UnityEngine.Transform>, RefRW<PlayerLevelChunkData>>().WithEntityAccess())
                {
                    var chunkId = quadtree.query(transform.Value.position).id;

                    if(playerLevelChunk.ValueRO.value != chunkId)
                    {
                        EventQueue.publish(new PlayerLevelChunkChangeEvent
                        {
                            player  = entity,
                            enter   = chunkId,
                            exit    = playerLevelChunk.ValueRO.value
                        });
                    }

                    playerLevelChunk.ValueRW.value = chunkId;
                }
		    }

            void destroyOldQuadtreeData()
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if(entityManager.CreateEntityQuery(typeof(LevelChunkQuadtree)).TryGetSingletonEntity<LevelChunkQuadtree>(out Entity entity))
                {
                    entityManager.GetComponentData<LevelChunkQuadtree>(entity).Dispose();
                    entityManager.DestroyEntity(entity);
                }
            }

            void onNewLevelGeneratedEvent(NewLevelGeneratedEvent e)
            {
                this.destroyOldQuadtreeData();
                World.DefaultGameObjectInjectionWorld.EntityManager.CreateSingleton<LevelChunkQuadtree>(new LevelChunkQuadtree(e.levelData));
            }

            void onEntitySpawnedEvent(EntitySpawnedEvent e)
            {
                var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                if(entityManager.HasComponent<Player>(e.entity))
                {
                    entityManager.AddComponentData<PlayerLevelChunkData>(e.entity, new PlayerLevelChunkData { value = LevelData.Chunk.INVALID.id });
                }
            }
        }

        public struct PlayerLevelChunkData : IComponentData
        {
            public int value;
        }
    }
}
