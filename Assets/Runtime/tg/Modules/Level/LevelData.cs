using System;
using System.Collections;
using System.Collections.Generic;

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using Unity.Entities;
using static tg.level.generator.Generator;
using System.Linq;

namespace tg.level
{
    /// <summary>
    /// A commonly shared data object between level generation steps. Each step may add its own
    /// relevant level data to the partial struct and can access data declared by other steps. 
    /// </summary>
    public partial struct LevelData : IDisposable
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
        public struct Chunk : IDisposable, IComponentData
        {
            /// <summary>
            /// Unique chunk id. Chunks are created in order, which means chunks with a smaller id have been created first.
            /// </summary>
            public readonly int                     id;

            /// <summary>
            /// Logical boundaries of a chunk in the virtual world.
            /// </summary>
            public RectInt                          bounds;

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
            /// Holds the index of the very first data entry in the global chunk data array for this particular chunk.
            /// </summary>
            public int                              dataIndex0;

            /// <summary>
            /// The number of data entries in the global data array (this is basically bounds.width * bounds.height).
            /// </summary>
            public int                              dataSize;

            /// <summary>
            /// Reference to the chunk owned tile data.
            /// </summary>
            public NativeSlice<Tile>                data;

            /// <summary>
            /// Creates a new chunk info element.
            /// </summary>
            /// <param name="chunkId"></param>
            public Chunk(int chunkId)
            {
                this.id             = chunkId;

                this.bounds         = default;
                this.wallSize       = 0;

                this.dataIndex0     = 0;
                this.dataSize       = 0;

                this.pathExit       = default;
                this.data           = default;
            }

            public void Dispose()
            {
                
            }
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
                Obstructed      = 1 << 0,

                /// <summary>
                /// Decales a tile beeing walkable, that is, no object is placed here to obstruct
                /// a moving entity.
                /// </summary>
                Walkable        = 1 << 1,

                /// <summary>
                /// Declares a tile not being part of the level.
                /// </summary>
                Empty           = 1 << 2
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

        /// <summary>
        /// The actual tile data array. Chunks will hold a reference to their slice of data into this array.
        /// </summary>
        private NativeArray<Tile>       data;

        public void addChunk(ref Chunk chunk)
        {
            if(!this.chunks.IsCreated)
            {
                this.chunks = new UnsafeList<Chunk>(8, Allocator.Persistent);
            }

            if(this.data.IsCreated)
            {
                var currentSize = this.data.Length;
                var newData     = new NativeArray<Tile>(Enumerable.Range(0, currentSize + chunk.dataSize).Select(x => Tile.Default).ToArray(), Allocator.Persistent);

                newData.Slice(0, currentSize).CopyFrom(this.data);
                this.data.Dispose();
                this.data       = newData;
            }
            else
            {
                this.data = new NativeArray<Tile>(Enumerable.Range(0, chunk.dataSize).Select(x => Tile.Default).ToArray(), Allocator.Persistent);
            }

            // since we have resized the data array, all chunk data slices are invalid and need to be updated
            for(int chunkId = 0; chunkId < this.chunks.Length; chunkId++)
            {
                ref var c       = ref this.chunks.ElementAt(chunkId);
                c.data          = this.data.Slice(c.dataIndex0, c.dataSize);
            }

            chunk.dataIndex0    = this.data.Length - chunk.dataSize;
            chunk.data          = this.data.Slice(chunk.dataIndex0, chunk.dataSize);
            this.chunks.Add(chunk);
        }

        public void Dispose()
        {
            // dispose of all chunk data
            if(this.chunks.IsCreated)
            {
                Debug.Log($"Dispose of {this.chunks.Length} level chunks.");
                foreach(var chunk in this.chunks) { chunk.Dispose(); }
                this.chunks.Dispose();
            }

            // dispose all tile data
            if(this.data.IsCreated)
            {
                Debug.Log($"Dispose of {this.data.Length} level tiles.");
                foreach(var tile in this.data) { tile.Dispose(); }
                this.data.Dispose();
            }
        }


        #region Convenient methods

        public int                      numChunks { get { return this.chunks.Length; } }

        /// <summary>
        /// Get chunk by its id.
        /// </summary>
        /// <param name="chunkId"></param>
        /// <returns></returns>
        public ref Chunk                getChunk(int chunkId) { return ref this.chunks.ElementAt(chunkId); }

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