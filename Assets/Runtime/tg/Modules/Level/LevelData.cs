using System;
using System.Linq;

using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using static tg.level.LevelData;

namespace tg.level
{
    /// <summary>
    /// A commonly shared data object between level generation steps. Each step may add its own
    /// relevant level data to the partial struct and can access data declared by other steps. 
    /// </summary>
    public partial struct LevelData : IComponentData, IDisposable
    {
        /// <summary>
        /// Level tiles can be build of multiple layers. Each layer has a certains index which determines, if a layer
        /// should be placed on top of the other.
        /// </summary>
        public struct Layer : IEquatable<Layer>, IComparable<Layer>
        {
            #region Layers

            public static Layer                 Floor                               = new Layer(0);
            public static Layer                 Obstructable                        = new Layer(1);
            // add more layer here...

            public static int                   MAX_LAYERS { get; private set; }    = 0;

            #endregion

            #region C'tor, internal data and logical operators

            private readonly int                index;

            private Layer(int index)
            {
                this.index = index;
                Layer.MAX_LAYERS++;
            }

            public override int GetHashCode() { return this.index.GetHashCode(); }

            public bool Equals(Layer other) { return this.index.Equals(other.index); }

            public int CompareTo(Layer other) { return this.index.CompareTo(other.index); }

            public static implicit operator int(Layer layer) { return layer.index; }
            public static implicit operator Layer(int id) { return new Layer(id); }

            #endregion
        }

        /// <summary>
        /// Auxilary struct that holds more information of a certain chunk in the
        /// global data array.
        /// </summary>
        public struct Chunk : IDisposable
        {
            /// <summary>
            /// Unique chunk id. Chunks are created in order, which means chunks with a smaller id have been created first.
            /// </summary>
            public int                              id { get; private set; }

            /// <summary>
            /// Logical boundaries of a chunk in the virtual world.
            /// </summary>
            public RectInt                          bounds;

            /// <summary>
            /// Holds the ids of accessable neighbouring chunks. If neighbour doesn't exist of is not accessable since its blocked
            /// through a wall the id is -1.
            /// </summary>
            public int4                             neighbours;

            /// <summary>
            /// Wall size in for a certain chunk
            /// </summary>
            public int                              wallSize;

            /// <summary>
            /// Since chunks are created in an order they are connected
            /// to their neighbour with an exit. This vector states on which
            /// side of the chunk bounds the exit is.
            /// </summary>
            public Vector2Int                       pathExit;

            /// <summary>
            /// Reference to the chunk owned tile data.
            /// </summary>
            public NativeArray<Tile>                data;

            public Tile                             this[int tileId]
            {
                get { return this.data[tileId]; }
                set { this.data[tileId] = value; }
            }

            public Tile                             this[int x, int y]
            {
                get { return this.data[(this.bounds.width * y) + x]; }
                set { this.data[(this.bounds.width * y) + x] = value; }
            }

            /// <summary>
            /// Creates a new chunk info element.
            /// </summary>
            /// <param name="chunkId"></param>
            internal Chunk(int chunkId, int width, int height)
            {
                this.id             = chunkId;
                this.neighbours     = -1;

                this.bounds         = default;
                this.wallSize       = 0;
                this.pathExit       = default;

                this.data           = new NativeArray<Tile>(Enumerable.Range(0, width * height).Select(x => Tile.Default).ToArray(), Allocator.Persistent);
            }

            public static readonly Chunk INVALID = new Chunk
            {
                id          = -1,
                neighbours  = -1,
                bounds      = default
            };

            public bool valid { get { return this.id != -1; } }

            public void Dispose()
            {
                if(this.data.IsCreated)
                {
                    foreach(var tile in this.data)
                    {
                        if(tile.IsCreated) { tile.Dispose(); }
                    }

                    this.data.Dispose();
                }
            }

            public int leftNeighbour   { get { return this.neighbours[0]; } set { this.neighbours[0] = value; } }
            public int rightNeighbour  { get { return this.neighbours[1]; } set { this.neighbours[1] = value; } }
            public int topNeighbour    { get { return this.neighbours[2]; } set { this.neighbours[2] = value; } }
            public int bottomNeighbour { get { return this.neighbours[3]; } set { this.neighbours[3] = value; } }
        }

        /// <summary>
        /// An alias type that is used to reference managed tile data objects.
        /// </summary>
        public struct Module : IEquatable<Module>
        {
            private static int   NEXT_VALID_MODULE_ID = 0;
            public  readonly int id;

            private Module(int id) { this.id = id; }

            public static readonly Module INVALID = new Module(-1);

            public static Module Create() { return new Module(Module.NEXT_VALID_MODULE_ID++); }

            public static implicit operator int(Module module) { return module.id; }
            public static implicit operator Module(int module) { return new Module(module); }

            public bool Equals(Module other) { return this.id.Equals(other.id); }
        }

        /// <summary>
        /// A tile is a single piece of the entire level. Many tiles make up the entire level.
        /// Each tile stores data like what module to render, if it is walkable etc.
        /// </summary>
        public struct Tile : IDisposable 
        {
            /// <summary>
            /// Defines the architectural intent of a tile.
            /// </summary>
            public enum ConstructionType
            {
                /// <summary>
                /// Used as a "joker" where a tile can become any of the bellow defined states.
                /// </summary>
                Undefined       = 0,

                /// <summary>
                /// Declares a tile as obstructed and no moving entity can pass it.
                /// </summary>
                Obstructed,

                /// <summary>
                /// Decales a tile beeing walkable, that is, no object is placed here to obstruct
                /// a moving entity.
                /// </summary>
                Walkable,

                /// <summary>
                /// Declares a tile not being part of the level.
                /// </summary>
                Empty
            }

            #region Tile data

            public ConstructionType                 constructionType;

            private UnsafeHashMap<Layer, Module>    layers;

            #endregion

            #region Create new tile

            public static Tile Default
            {
                get
                {
                    return new Tile
                    {
                        constructionType    = ConstructionType.Undefined,
                        layers              = new UnsafeHashMap<Layer, Module>(4, Allocator.Persistent)
                    };
                }
            }

            public static Tile Empty
            {
                get
                {
                    return new Tile
                    {
                        constructionType    = ConstructionType.Empty,
                        layers              = default
                    };
                }
            }

            #endregion

            #region Clean-up

            public bool IsCreated { get { return this.layers.IsCreated; } }
            public void Dispose()
            {
                if(this.layers.IsCreated) { this.layers.Dispose(); }
            }

            #endregion

            /// <summary>
            /// Access the module data on a certain layer of this tile.
            /// </summary>
            /// <param name="layer"></param>
            /// <returns></returns>
            public Module this[Layer layer]
            {
                get { return this.layers.ContainsKey(layer) ? this.layers[layer] : Module.INVALID; }

                set { this.layers[layer] = value; }
            }
        }

        /// <summary>
        /// Seperate parts (chunks) of the entire level. Basically rooms.
        /// </summary>
        internal UnsafeList<Chunk>      chunks;

        private int                     nextChunkId;

        public ref Chunk createNewChunk(int width, int height)
        {
            if(!this.chunks.IsCreated)
            {
                this.chunks = new UnsafeList<Chunk>(8, Allocator.Persistent);
            }

            var newChunk = new LevelData.Chunk(this.nextChunkId++, width, height);

            this.chunks.Add(newChunk);
            return ref this.chunks.ElementAt(this.chunks.Length - 1);
        }

        public void Dispose()
        {
            // dispose of all chunk data
            if(this.chunks.IsCreated)
            {
                Debug.Log($"Dispose of {this.chunks.Length} level chunks [{this.chunks.Length * this.chunks[0].data.Length} tiles].");
                foreach(var chunk in this.chunks) { chunk.Dispose(); }
                this.chunks.Dispose();
            }

            this.nextChunkId = 0;
        }

        #region Convenient methods

        public int                      numChunks { get { return this.chunks.Length; } }

        /// <summary>
        /// Get chunk by its id.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        public ref Chunk                getChunk(int index) { return ref this.chunks.ElementAt(index); }
        public ref Chunk                getChunkById(int id)
        {
            for(int i = 0; i < this.numChunks; i++)
            {
                ref var chunk = ref this.chunks.ElementAt(i);
                if(chunk.id > id)
                {
                    break;
                }

                if(chunk.id == id)
                {
                    return ref chunk;
                }
            }

            throw new Exception($"Chunk with id {id} does not exist.");
        }

        /// <summary>
        /// Retruns the first matching chunk at this position.
        /// </summary>
        /// <param name="position"></param>
        /// <returns></returns>
        public Chunk?                   getChunk(Vector2Int position)
        {
            foreach(var chunkInfo in this.chunks)
            {
                if(chunkInfo.bounds.Contains(position))
                {
                    return chunkInfo;
                }
            }

            return null;
        }

        #endregion
    }
}