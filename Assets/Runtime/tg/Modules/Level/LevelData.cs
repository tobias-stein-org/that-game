using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace tg.level
{
    /// <summary>
    /// A commonly shared data object between level generation steps. Each step may add its own
    /// relevant level data to the partial struct and can access data declared by other steps. 
    /// </summary>
    public partial struct LevelData
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

            #endregion
        }

        /// <summary>
        /// Auxilary struct that holds more information of a certain chunk in the
        /// global data array.
        /// </summary>
        public struct ChunkInfo
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
            /// Creates a new chunk info element.
            /// </summary>
            /// <param name="chunkId"></param>
            public ChunkInfo(int chunkId)
            {
                this.id             = chunkId;

                this.bounds         = default;
                this.wallSize       = 0;

                this.dataIndex0     = 0;
                this.dataSize       = 0;

                this.pathExit       = default;
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
    }
}