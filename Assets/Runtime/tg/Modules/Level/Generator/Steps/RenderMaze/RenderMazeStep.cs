using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace tg.level
{
    namespace generator.step
    {
        using tg.util;

        /// <summary>
        /// This generator step depends on the CreateMaze step. It will leverage the
        /// Wave-Function-Collapse (WFC) algorithm to render each tile in the maze giving a
        /// set of constraints and input modules (sprites).
        /// </summary>
        [BurstCompile]
        public class RenderMazeStep : LevelGeneratorStep<RenderMazeStepSettings>
        {
            public  const float                     EPSILONE = 1e-6f;


            /// <summary>
            /// Utitliy struct that stores the current state of the WFC algorithm. This state
            /// will be shared among jobs.
            /// </summary>
            internal struct StateWFC
            {
                private UnsafeBitArray              state;

                public  bool IsCreated { get { return this.state.IsCreated; } }
                public  void Dispose()
                {
                    if(this.state.IsCreated) { this.state.Dispose(); }
                }

                public static StateWFC    Create()
                {
                    return new StateWFC
                    {
                        state = new UnsafeBitArray(32, Allocator.Persistent, NativeArrayOptions.ClearMemory)
                    };
                }

                public void reset()
                {
                    this.state.Clear();
                }

                // BIT 1
                public bool hasSolution() { return this.state.IsSet(0); }
                public void hasSolution(bool value) { this.state.Set(0, value); }

                // BIT 2
                public bool hasFailed() { return this.state.IsSet(1); }
                public void hasFailed(bool value, bool unrecoverable = false)
                {
                    this.state.Set(1, value);
                    this.state.Set(3, unrecoverable);
                }

                // BIT 3
                public bool allCollapsed() { return this.state.IsSet(2); }
                public void allCollapsed(bool value) { this.state.Set(2, value); }

                // BIT 4
                public bool isRecoverable() { return !this.state.IsSet(3); }
                public void isRecoverable(bool value) { this.state.Set(3, !value); }

                // BIT 16-32
                public int  numFails() { return (int)this.state.GetBits(16, 16); }

                public void fail()
                {
                    this.hasFailed(true);
                    this.state.SetBits(16, (ulong)(this.numFails() + 1), 16);
                }
            }

            public  struct ModuleMeta : IDisposable
            {
                public LevelData.Module                 module { get; private set; }

                [ReadOnly]
                public UnsafeList<Color>                pixels;

                [ReadOnly]
                public Vector2Int                       textureSize;

                [ReadOnly]
                public float                            weight;

                [ReadOnly]
                public UnsafeText                       name;

                [ReadOnly]
                public LevelData.Tile.ConstructionType  constructionType;

                public bool IsCreated { get { return this.pixels.IsCreated || this.name.IsCreated; } }
                public void Dispose()
                {
                    if(this.pixels.IsCreated) { this.pixels.Dispose(); }
                    if(this.name.IsCreated) { this.name.Dispose(); }
                }

                public static implicit operator ModuleMeta(Module tile)
                {
                    unsafe
                    {
                        var x       = Mathf.FloorToInt(tile.sprite.textureRect.x);
                        var y       = Mathf.FloorToInt(tile.sprite.textureRect.y);
                        var width   = Mathf.FloorToInt(tile.sprite.textureRect.width);
                        var height  = Mathf.FloorToInt(tile.sprite.textureRect.height);

                        var pixels  = new UnsafeList<Color>(width * height, Allocator.Persistent);
                        foreach(var pixel in tile.sprite.texture.GetPixels(x, y, width, height)) { pixels.AddNoResize(pixel); }

                        var name = new UnsafeText(tile.name.Length, Allocator.Persistent);
                        name.CopyFrom(tile.name);

                        return new ModuleMeta {
                            module              = LevelData.Module.Create(),
                            pixels              = pixels,
                            textureSize         = new Vector2Int(width, height),
                            weight              = tile.weight,
                            name                = name,
                            constructionType    = tile.constructionType
                        };
                    }
                }
            }

            [BurstCompile]
            public  struct ModuleConstraints : IDisposable
            {
                //public enum Side { Left = 0, Right, Top, Bottom }

                public struct Side : IEquatable<Side>
                {
                    private int side;

                    private Side(int side) { this.side = side; }

                    public bool Equals(Side other) { return this.side.Equals(other.side); }

                    public static implicit operator int(Side side) { return side.side; }
                    public static implicit operator Side(int side) { return new Side(side); }

                    public const int Left    = 0;
                    public const int Right   = 1;
                    public const int Top     = 2;
                    public const int Bottom  = 3;
                }

                private UnsafeBitArray                          constraints;

                private int                                     numModules;
                private int                                     rowLength;

                public ModuleConstraints([ReadOnly]NativeArray<ModuleMeta>.ReadOnly modules)
                {
                    this.numModules  = modules.Length;
                    this.rowLength   = modules.Length * modules.Length;
                    this.constraints = new UnsafeBitArray(this.rowLength * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                }


                public bool IsCreated { get { return this.constraints.IsCreated; } }

                public void Dispose()
                {
                    if(this.constraints.IsCreated) { this.constraints.Dispose(); }
                }

                /// <summary>
                /// Allow/disallow moduleA to be on the "side" of moduleB. This will automatically also allow the other way around, that is,
                /// allow/disallow moduleB to be on the opposite-"side" of moduleA.
                /// </summary>
                /// <param name="moduleB"></param>
                /// <param name="moduleA"></param>
                /// <param name="side"></param>
                public void set(in LevelData.Module moduleA, in Side side, in LevelData.Module moduleB, bool allow = true)
                {
                    var moduleARowStartIndex = moduleA * this.numModules;
                    var moduleBRowStartIndex = moduleB * this.numModules;

                    switch(side)
                    {
                        case Side.Left:
                        {
                            this.constraints.Set(Side.Left   * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                            this.constraints.Set(Side.Right  * this.rowLength + moduleARowStartIndex + moduleB, allow);
                            break;
                        }

                        case Side.Right:
                        {
                            this.constraints.Set(Side.Right  * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                            this.constraints.Set(Side.Left   * this.rowLength + moduleARowStartIndex + moduleB, allow);
                            break;
                        }

                        case Side.Top:
                        {
                            this.constraints.Set(Side.Top    * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                            this.constraints.Set(Side.Bottom * this.rowLength + moduleARowStartIndex + moduleB, allow);
                            break;
                        }

                        case Side.Bottom:
                        {
                            this.constraints.Set(Side.Bottom * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                            this.constraints.Set(Side.Top    * this.rowLength + moduleARowStartIndex + moduleB, allow);
                            break;
                        }
                    }
                }

                /// <summary>
                /// Is moduleA allowed to be on the side of moduleB?
                /// </summary>
                /// <param name="moduleA"></param>
                /// <param name="side"></param>
                /// <param name="moduleB"></param>
                /// <returns></returns>
                public ulong isSet(in LevelData.Module moduleA, in Side side, in LevelData.Module moduleB)
                {
                    return this.constraints.GetBits((side * this.rowLength) + (moduleB * this.numModules) + moduleA);
                }
            }

            [BurstCompile]
            public  struct Grid : IDisposable
            {
                [NativeDisableContainerSafetyRestriction]
                private UnsafeHashMap<LevelData.Layer, UnsafeList<GridCell>>    layers;

                public  bool                    IsCreated   { get { return this.layers.IsCreated; } }

                public  int                     width       { get; private set; }
                public  int                     height      { get; private set; }
                public  int                     size        { get { return this.width * this.height; } }

                public static Grid Create(int width, int height, IEnumerable<LevelData.Layer> layers = null)
                {
                    var instance = new Grid
                    {
                        width   = width,
                        height  = height,
                        layers  = new UnsafeHashMap<LevelData.Layer, UnsafeList<GridCell>>(LevelData.Layer.MAX_LAYERS, Allocator.Persistent)
                    };

                    if(layers != null) { foreach(var layer in layers) { instance.addLayer(layer); } }

                    return instance;
                }

                public void Dispose()
                {
                    if(this.layers.IsCreated)
                    {
                        foreach(var layer in this.layers)
                        {
                            if(layer.Value.IsCreated) { layer.Value.Dispose(); }
                        }

                        this.layers.Dispose();
                    }
                }

                public void addLayer(LevelData.Layer layer)
                {
                    if(this.layers.ContainsKey(layer))
                    {
                        throw new Exception($"trying to add layer {layer} twice to grid.");
                    }

                    // pre-allocate memory for all grid cells
                    var cells       = new UnsafeList<GridCell>(this.size, Allocator.Persistent);

                    // and populate them with empty cells
                    foreach(var emptyCell in Enumerable.Range(0, this.size).Select(_ => GridCell.Empty)) { cells.AddNoResize(emptyCell); }

                    this.layers[layer] = cells;
                }

                public void reset(LevelData.Layer layer)
                {
                    var cells = this.layers[layer];
                    for(int cellId = 0; cellId < this.size; cellId++) { cells.ElementAt(cellId).reset(); } 
                }

                #region Grid cell access

                public ref GridCell this[LevelData.Layer layer, int index] { get { return ref this.layers[layer].ElementAt(index); } }

                #endregion // Grid cell access
            }

            [BurstCompile]
            public  unsafe struct GridCell
            {
                public  bool                isEmpty { get; private set; }
                public  bool                isCollapsed;
                private bool                canReset;

                public  LevelData.Module    module;
                public  float               entropy;

                private LevelData.Layer     layer;
                private LevelData.Tile*     tiles;

                public static GridCell Create(LevelData.Tile* tiles, LevelData.Layer layer)
                {
                    // get the current moduleId set on this cell, this will most of the time be -1, but in case we are dealing with a neighbouring tile
                    // of the current processed chunk, it could already been processed and set to a non negative value
                    var module = (*tiles)[layer];
           
                    return new GridCell
                    {
                        isEmpty                 = false,

                        // note: we make an assumption here, that if the tile module of the current referenced tile is unset or set
                        // it will also be the case for all other layers
                        canReset                = module == LevelData.Module.INVALID,
                        isCollapsed             = module != LevelData.Module.INVALID ? true : false,
                        module                  = module,
                        entropy                 = 0.0f,

                        layer                   = layer,
                        tiles                   = tiles,
                    };
                }

                public static GridCell Empty
                {
                    get
                    {
                        return new GridCell
                        {
                            isEmpty             = true,

                            canReset            = false,
                            isCollapsed         = false,
                            module              = LevelData.Module.INVALID,
                            entropy             = 0.0f,

                            tiles               = null,
                        };
                    }
                }

                public void reset()
                {
                    if(this.canReset)
                    {
                        this.isCollapsed        = false;
                        this.entropy            = 0.0f;
                        this.module             = LevelData.Module.INVALID;
                    }
                }

                public void apply()
                {
                    if(this.isEmpty)
                    {
                        throw new Exception($"Cannot apply GridCell changes to an empty cell.");
                    }

                    var tile                 = this.tile;
                    tile[this.layer]         = this.module;
                    this.tile                = tile;
                }

                public LevelData.Tile tile
                {
                    get         { return *(this.tiles); }
                    private set { *(this.tiles) = value; }
                }
            }

            public  struct GridCellModuleWeightsUndoBuffer : IDisposable
            {
                private NativeArray<float>      buffer;
                private NativeReference<int>    bufferIndex;
                private NativeList<int>         undos;

                private readonly int            chunkSize;
                private readonly int            numModules;
                private readonly int            historySize;

                public GridCellModuleWeightsUndoBuffer(in Grid grid, in NativeArray<ModuleMeta> modules, int undoHistorySize = 1)
                {
                    this.chunkSize      = grid.size * modules.Length;

                    this.buffer         = new NativeArray<float>(this.chunkSize * undoHistorySize, Allocator.Persistent, NativeArrayOptions.ClearMemory);
                    this.bufferIndex    = new NativeReference<int>(0, Allocator.Persistent);
                    this.undos          = new NativeList<int>(undoHistorySize, Allocator.Persistent);

                    this.numModules     = modules.Length;
                    this.historySize    = undoHistorySize;
                }

                public void apply()
                {
                    if(this.historySize < 2) { return; }


                    if(this.undos.Length + 1 < this.historySize)
                    {
                        this.undos.AddNoResize(this.bufferIndex.Value);
                    }
                    else
                    {
                        // remove oldest from history
                        this.undos.RemoveAt(0);
                        this.undos.AddNoResize(this.bufferIndex.Value);
                    }

                    var nextBufferIndex = (this.bufferIndex.Value + 1) % this.historySize;

                    var src = this.buffer.Slice(this.bufferIndex.Value * this.chunkSize, this.chunkSize);
                    var dst = this.buffer.Slice(nextBufferIndex  * this.chunkSize, this.chunkSize);
                    dst.CopyFrom(src);

                    this.bufferIndex.Value      = nextBufferIndex;
                }

                public void undo()
                {
                    var lastIndex               = this.undos.Length - 1;
                    this.bufferIndex.Value      = this.undos[lastIndex];
                    this.undos.RemoveAt(lastIndex);
                }

                public GridCellModuleWeights current    { get { return new GridCellModuleWeights(this.buffer.Slice(this.bufferIndex.Value                          * this.chunkSize, this.chunkSize), this.numModules); } }
                public GridCellModuleWeights next       { get { return new GridCellModuleWeights(this.buffer.Slice((this.bufferIndex.Value + 1) % this.historySize * this.chunkSize, this.chunkSize), this.numModules); } }

                public bool canUndo     { get { return this.undos.Length > 0; } }

                public bool IsCreated   { get { return this.buffer.IsCreated; } }

                public void Dispose()
                {
                    if(this.buffer.IsCreated)
                    {
                        this.buffer.Dispose();
                        this.bufferIndex.Dispose();
                        this.undos.Dispose();
                    }
                }
            }

            [BurstCompile]
            public  struct GridCellModuleWeights
            {
                [NativeDisableUnsafePtrRestriction]
                private readonly unsafe void*       weights;
                private readonly int                numModules;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                private readonly AtomicSafetyHandle weightsNativeSafetyHandle;
#endif
                public GridCellModuleWeights(in NativeArray<float> weights, int numModules)
                {
                    unsafe
                    {
                        this.weights                    = weights.GetUnsafePtr();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        this.weightsNativeSafetyHandle  = NativeArrayUnsafeUtility.GetAtomicSafetyHandle(weights);
#endif
                    }

                    this.numModules                     = numModules;
                }

                public GridCellModuleWeights(in NativeSlice<float> weights, int numModules)
                {
                    unsafe
                    {
                        this.weights                    = weights.GetUnsafePtr();
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        this.weightsNativeSafetyHandle  = NativeSliceUnsafeUtility.GetAtomicSafetyHandle(weights);
#endif
                    }

                    this.numModules                     = numModules;
                }

                public NativeSlice<float> cellWeights(int cellId)
                {
                    unsafe
                    {
                        var slice = NativeSliceUnsafeUtility.ConvertExistingDataToNativeSlice<float>((float*)this.weights + (cellId * this.numModules), sizeof(float), this.numModules);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        // note: this is necessary to ensure all Unity Collection safety checks pass. It basically states that this slice object will be valid as long as the array is valid.
                        NativeSliceUnsafeUtility.SetAtomicSafetyHandle(ref slice, this.weightsNativeSafetyHandle);
#endif
                        return slice;
                    }
                }
            }

            [BurstCompile]
            private struct InitializeConstraintsJob : IJobParallelFor
            {
                public float                    similarityThreshold;
                public float                    similarityPercentile;

                [NativeDisableParallelForRestriction] 
                public ModuleConstraints        constraints;

                [NativeDisableParallelForRestriction]
                public NativeArray<ModuleMeta>  modules;

                /*
                            0               2 3               5 6               8
                            <-  M1 constr. -> <-  M2 constr. -> <-  M2 constr. ->
                            M1/M1 M1/M2 M1/M3 M2/M1 M2/M2 M2/M3 M3/M1 M3/M2 M3/M3
                    left      0     1    x
                    right                                         x
                    top                         y
                    bottom          y


                    note: constraint matrix is mirrored, meaning if a match is possible on Mi/Mj left -> Mj/Mi right is also true
                    x, y = {0, 1}
                 */

                [BurstCompile]
                public void Execute(int moduleA)
                {
                    for(int moduleB = moduleA; moduleB < this.modules.Length; moduleB++)
                    {
                        this.check(moduleA, ModuleConstraints.Side.Left, moduleB);
                        this.check(moduleA, ModuleConstraints.Side.Top, moduleB);

                        if(moduleA != moduleB)
                        {
                            this.check(moduleA, ModuleConstraints.Side.Right, moduleB);
                            this.check(moduleA, ModuleConstraints.Side.Bottom, moduleB);
                        }
                    }

                    this.adjustModuleWeight(moduleA);
                }

                private void adjustModuleWeight(in LevelData.Module moduleA)
                {
                    var module              = this.modules[moduleA];

                    int numModules          = this.modules.Length;
                    int sumOfOnes           = 0;


                    for(int moduleB = 0; moduleB < numModules; moduleB++)
                    {
                        sumOfOnes           += (int)this.constraints.isSet(moduleA, ModuleConstraints.Side.Left, moduleB);
                        sumOfOnes           += (int)this.constraints.isSet(moduleA, ModuleConstraints.Side.Right, moduleB);
                        sumOfOnes           += (int)this.constraints.isSet(moduleA, ModuleConstraints.Side.Top, moduleB);
                        sumOfOnes           += (int)this.constraints.isSet(moduleA, ModuleConstraints.Side.Bottom, moduleB);
                    }

                    module.weight           = module.weight * ((float)sumOfOnes / (float)Mathf.Max(0, numModules * 4));
                    this.modules[moduleA]   = module;
                }

                private float percentile(NativeArray<float> values, float percentile)
                {
                    // Sort the array of values in ascending order
                    values.Sort();

                    // Calculate the index corresponding to the desired percentile
                    var index           = percentile / 100.0f * (values.Length - 1);

                    // Separate the whole and fractional parts of the index
                    var lowerIndex      = Mathf.FloorToInt(index);
                    var fractionalPart  = index - lowerIndex;

                    // Interpolate the percentile value
                    var lowerValue      = values[lowerIndex];
                    var upperValue      = values[lowerIndex + 1];

                    return lowerValue + (upperValue - lowerValue) * fractionalPart;
                }

                private void applyBlur1D(NativeArray<Color> pixels, int blurSize, float blurStrength)
                {
                    var blurredPixels           = new NativeArray<Color>(pixels.Length, Allocator.Temp);

                    for (int p = 0; p < pixels.Length; p++)
                    {
                        var accumulatedColor    = Color.black;
                        var totalWeight         = 0f;

                        for (int i = -blurSize; i <= blurSize; i++)
                        {
                            var offsetX         = Mathf.Clamp(p + i, 0, pixels.Length - 1);
                            var pixel           = pixels[offsetX];
                            var weight          = Mathf.Exp(-i * i / (2f * blurStrength * blurStrength));

                            accumulatedColor    += pixel * weight;
                            totalWeight         += weight;
                        }

                        blurredPixels[p]        = accumulatedColor / totalWeight;
                        // ignore alpha bluring
                        blurredPixels[p]        = new Color(blurredPixels[p].r, blurredPixels[p].g, blurredPixels[p].b, pixels[p].a);
                    }

                    pixels.CopyFrom(blurredPixels);

                    blurredPixels.Dispose();
                }

                private void check(in LevelData.Module moduleA, in ModuleConstraints.Side side, in LevelData.Module moduleB)
                {
                    var mA          = this.modules[moduleA];
                    var mB          = this.modules[moduleB];

                    var width       = mA.textureSize.x;
                    var height      = mA.textureSize.y;

                    var similar     = new NativeArray<float>();
                    var borderA     = new NativeArray<Color>();
                    var borderB     = new NativeArray<Color>();

                    switch(side)
                    {
                        case ModuleConstraints.Side.Left:
                        {
                            borderA = new NativeArray<Color>(height, Allocator.Temp);
                            borderB = new NativeArray<Color>(height, Allocator.Temp);
                            similar = new NativeArray<float>(height, Allocator.Temp);

                            var rightBorderOffset   = width - 1;
                            for(int py = 0; py < height; py++)
                            {
                                borderA[py] = mA.pixels[py * width + rightBorderOffset]; // moduleA's right border
                                borderB[py] = mB.pixels[py * width];                     // moduleB's left border
                            }

                            break;
                        }

                        case ModuleConstraints.Side.Right:
                        {
                            borderA = new NativeArray<Color>(height, Allocator.Temp);
                            borderB = new NativeArray<Color>(height, Allocator.Temp);
                            similar = new NativeArray<float>(height, Allocator.Temp);

                            var rightBorderOffset   = width - 1;
                            for(int py = 0; py < height; py++)
                            {
                                borderA[py] = mA.pixels[py * width];                     // moduleA's left border
                                borderB[py] = mB.pixels[py * width + rightBorderOffset]; // moduleB's right border
                            }

                            break;
                        }

                        case ModuleConstraints.Side.Bottom:
                        {
                            borderA = new NativeArray<Color>(width, Allocator.Temp);
                            borderB = new NativeArray<Color>(width, Allocator.Temp);
                            similar = new NativeArray<float>(width, Allocator.Temp);

                            // note: unity stores texture data "up-side-down", that is the first pixel is bottom-left!
                            var topBorderOffset  = (height - 1) * width;

                            for(int px = 0; px < width; px++)
                            {
                                borderA[px] = mA.pixels[px];                            // moduleA's bottom border
                                borderB[px] = mB.pixels[px + topBorderOffset];          // moduleB's top border
                            }

                            break;
                        }

                        case ModuleConstraints.Side.Top:
                        {
                            borderA = new NativeArray<Color>(width, Allocator.Temp);    
                            borderB = new NativeArray<Color>(width, Allocator.Temp);    
                            similar = new NativeArray<float>(width, Allocator.Temp);    

                            // note: unity stores texture data "up-side-down", that is the first pixel is bottom-left!
                            var topBorderOffset  = (height - 1) * width;
                            for(int px = 0; px < width; px++)
                            {
                                borderA[px] = mA.pixels[px + topBorderOffset];          // moduleA's top border
                                borderB[px] = mB.pixels[px];                            // moduleB's bottom border
                            }

                            break;
                        }
                    }
                    this.applyBlur1D(borderA, borderA.Length, borderA.Length);
                    this.applyBlur1D(borderB, borderB.Length, borderB.Length);
                    for(int i = 0; i < borderA.Length; i++)
                    {
                        // allow modules of different construction types, e.g. floor and obstructable to overlap, if there is a transparent edge
                        if(this.modules[moduleA].constructionType != this.modules[moduleB].constructionType)
                        {
                            float minAlpha  = Mathf.Min(borderA[i].a, borderB[i].a);
                            borderA[i] = minAlpha < 1.0f ? Color.Lerp(borderB[i], borderA[i], minAlpha) : borderA[i];
                        }
                        else
                        {
                            borderA[i] = borderA[i].a < 1.0f ? Color.magenta : borderA[i];
                            borderB[i] = borderB[i].a < 1.0f ? Color.magenta : borderB[i];
                        }

                        
                        similar[i] = Mathf.Clamp(borderA[i].similarity(borderB[i]), 0.0f, this.similarityThreshold);
                    }

                    float pct = this.percentile(similar, this.similarityPercentile);

                    //Debug.Log($"{moduleA}-{side.ToString()[0]}-{moduleB}: {similarity}");
                    // note: lower CIEDE2000 values are more similar, values bellow one are considered hardly distigushable by the human eye
                    this.constraints.set(moduleA, side, moduleB, pct < this.similarityThreshold);


                    similar.Dispose();
                    borderA.Dispose();
                    borderB.Dispose();
                }
            }

            [BurstCompile]
            private struct InitializeGridJob : IJobParallelForBatch
            {
                [NativeDisableParallelForRestriction]
                public  Grid                            grid;
                public  LevelData.Layer                 layer;

                [ReadOnly]
                public  LevelData.Chunk                 chunk;

                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkLeft;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkRight;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkTop;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkBottom;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkTopLeft;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkTopRight;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkBottomLeft;
                [ReadOnly]
                public  NativeSlice<LevelData.Tile>     chunkBottomRight;

                private bool                            hasLeft        { get { return this.chunkLeft.Length        > 0; } }
                private bool                            hasRight       { get { return this.chunkRight.Length       > 0; } }
                private bool                            hasTop         { get { return this.chunkTop.Length         > 0; } }
                private bool                            hasBottom      { get { return this.chunkBottom.Length      > 0; } }

                private bool                            hasTopLeft     { get { return this.chunkTopLeft.Length     > 0; } }
                private bool                            hasTopRight    { get { return this.chunkTopRight.Length    > 0; } }
                private bool                            hasBottomLeft  { get { return this.chunkBottomLeft.Length  > 0; } }
                private bool                            hasBottomRight { get { return this.chunkBottomRight.Length > 0; } }

                public void Execute(int startIndex, int gridWidth)
                {
                    unsafe
                    {
                        int y = startIndex / gridWidth;

                        for(int x = 0; x < this.grid.width; x++)
                        {
                            var cellId = (y * this.grid.width) + x;

                            // top-left
                            if(x == 0                   && y == 0                    && this.hasLeft  && this.hasTop)
                            {
                                this.grid[this.layer, cellId] = this.hasTopLeft ? GridCell.Create((LevelData.Tile*)this.chunkTopLeft.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                                continue;
                            }
                            // top-right
                            if(x == this.grid.width - 1 && y == 0                    && this.hasRight && this.hasTop)
                            {
                                this.grid[this.layer, cellId] = this.hasTopRight ? GridCell.Create((LevelData.Tile*)this.chunkTopRight.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                                continue;
                            }
                            // bottom-left
                            if(x == 0                   && y == this.grid.height - 1 && this.hasLeft  && this.hasBottom)
                            {
                                this.grid[this.layer, cellId] = this.hasBottomLeft ? GridCell.Create((LevelData.Tile*)this.chunkBottomLeft.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                                continue;
                            }
                            // bottom-right
                            if(x == this.grid.width - 1 && y == this.grid.height - 1 && this.hasRight && this.hasBottom)
                            {
                                this.grid[this.layer, cellId] = this.hasBottomRight ? GridCell.Create((LevelData.Tile*)this.chunkBottomRight.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                                continue;
                            }

                            // chunk adjusted tile (x,y) coord
                            var cx                              = this.hasLeft ? x - 1 : x;
                            var cy                              = this.hasTop  ? y - 1 : y;

                            // left
                            if(this.hasLeft             && x == 0)
                            {
                                this.grid[this.layer, cellId]   = GridCell.Create((LevelData.Tile*)this.chunkLeft.GetUnsafeReadOnlyPtr() + ((cy * this.chunk.bounds.width) + this.chunk.bounds.width - 1), this.layer);
                                continue;
                            }

                            // right
                            if(this.hasRight            && x == this.grid.width - 1)
                            {
                                this.grid[this.layer, cellId]   = GridCell.Create((LevelData.Tile*)this.chunkRight.GetUnsafeReadOnlyPtr() + (cy * this.chunk.bounds.width), this.layer);
                                continue;
                            }

                            // top
                            if(this.hasTop              && y == 0)
                            {
                                this.grid[this.layer, cellId]   = GridCell.Create((LevelData.Tile*)this.chunkTop.GetUnsafeReadOnlyPtr() + cx, this.layer);
                                continue;
                            }

                            // bottom
                            if(this.hasBottom           && y == this.grid.height - 1)
                            {
                                this.grid[this.layer, cellId]   = GridCell.Create((LevelData.Tile*)this.chunkBottom.GetUnsafeReadOnlyPtr() + cx, this.layer);
                                continue;
                            }

                            // else process current chunk data
                            var chunkTileId                     = (cy * this.chunk.bounds.width) + cx;
                            this.grid[this.layer, cellId]       = GridCell.Create((LevelData.Tile*)this.chunk.data.GetUnsafeReadOnlyPtr() + chunkTileId, this.layer);
                        }
                    }
                }
            }

            [BurstCompile]
            private struct InitializePassOneWeightsJob : IJobParallelForBatch
            {
                [ReadOnly]
                public NativeArray<ModuleMeta>          modules;

                [NativeDisableParallelForRestriction]
                [NativeDisableContainerSafetyRestriction]
                public GridCellModuleWeightsUndoBuffer  weightsBuffer;

                [ReadOnly]
                public Grid                             grid;
                public LevelData.Layer                  layer;

                public void Execute(int start, int count)
                {
                    var weights = this.weightsBuffer.current;

                    for(int cellId = start; cellId < start + count; cellId++)
                    {
                        var cell = this.grid[this.layer, cellId];

                        if(cell.isEmpty || cell.isCollapsed) { continue; }

                        var cellWeigths = weights.cellWeights(cellId);
                        for(int moduleId = 0; moduleId < this.modules.Length; moduleId++)
                        {
                            // ONLY, allow 'walkable' modules
                            cellWeigths[moduleId] = this.modules[moduleId].constructionType == LevelData.Tile.ConstructionType.Walkable
                                ? this.modules[moduleId].weight
                                : 0.0f;
                        }
                    }
                }
            }

            [BurstCompile]
            private struct InitializePassTwoWeightsJob : IJobParallelForBatch
            {
                [ReadOnly]
                public NativeArray<ModuleMeta>          modules;

                [NativeDisableParallelForRestriction]
                [NativeDisableContainerSafetyRestriction]
                public GridCellModuleWeightsUndoBuffer  weightsBuffer;

                [ReadOnly]
                public Grid                             grid;
                public LevelData.Layer                  layer;

                public float                            mapChunkObstructivness;

                public void Execute(int start, int count)
                {
                    var weights = this.weightsBuffer.current;

                    for(int cellId = start; cellId < start + count; cellId++)
                    {
                        var cell = this.grid[this.layer, cellId];

                        if(cell.isEmpty || cell.isCollapsed) { continue; }


                        var cellModuleWeight0   = (cellId * this.modules.Length);
                        var tileData            = cell.tile;

                        var cellWeights         = weights.cellWeights(cellId);
                        for(int moduleId = 0; moduleId < this.modules.Length; moduleId++)
                        {
                            var moduleConstructionType = this.modules[moduleId].constructionType;

                            switch(tileData.constructionType)
                            {
                                // force only obstructable modules to be placed here
                                case LevelData.Tile.ConstructionType.Obstructed:
                                {
                                    cellWeights[moduleId] = moduleConstructionType == LevelData.Tile.ConstructionType.Obstructed
                                        ? this.modules[moduleId].weight
                                        : 0.0f;
                                    break;
                                }

                                // never allow obstructables on walkable tiles
                                case LevelData.Tile.ConstructionType.Walkable:
                                {
                                    cellWeights[moduleId] = moduleConstructionType == LevelData.Tile.ConstructionType.Obstructed
                                        ? 0.0f
                                        : this.modules[moduleId].weight;
                                    break;
                                }

                                // else everything goes
                                default:
                                {
                                    cellWeights[moduleId] = moduleConstructionType == LevelData.Tile.ConstructionType.Obstructed
                                            ? this.modules[moduleId].weight * this.mapChunkObstructivness
                                            : this.modules[moduleId].weight * (1.0f - this.mapChunkObstructivness);
                                    break;
                                }
                            }
                    
                        }
                    }
                }
            }

            [BurstCompile]
            private struct PassTwoPostJob : IJobParallelForBatch
            {
                [ReadOnly]
                public NativeArray<ModuleMeta>          modules;

                public Grid                             grid;
                public LevelData.Layer                  layer;

                public void Execute(int startIndex, int count)
                {
                    for(int i = startIndex; i < startIndex + count; i++)
                    { 
                        if(!this.grid[this.layer, i].isEmpty && this.grid[this.layer, i].module != -1 && this.modules[this.grid[this.layer, i].module].constructionType != LevelData.Tile.ConstructionType.Obstructed)
                        {
                            this.grid[this.layer, i].module = -1;
                            this.grid[this.layer, i].apply();
                        }
                    }
                }
            }

            [BurstCompile]
            private struct ComputeEntropiesJob : IJobParallelForBatch
            {
                public Grid                                 grid;
                public LevelData.Layer                      layer;

                [NativeDisableContainerSafetyRestriction]
                [ReadOnly]
                public GridCellModuleWeightsUndoBuffer      weightsBuffer;

                public void Execute(int startIndex, int count)
                {
                    var current = this.weightsBuffer.current;

                    for(int i = 0; i < count; i++)
                    {
                        var cellId  = startIndex + i;

                        // empty or collapsed cells are ignored
                        if(this.grid[this.layer, cellId].isEmpty || this.grid[this.layer, cellId].isCollapsed) { continue; }

                        var weights                             = current.cellWeights(cellId);

                        //shannon_entropy_for_square = log(sum(weight)) - (sum(weight * log(weight)) / sum(weight))
                        // note: We use EPSILONE to deal with possible zero weights, which would results in NaN values in the log
                        float sum                               = 0.0f;
                        float logSum                            = 0.0f;
                        for(int w = 0; w < weights.Length; w++)
                        {
                            sum                                 += weights[w];
                            logSum                              += weights[w] * fastLog2(weights[w]);
                        }

                        // update cell
                        this.grid[this.layer, cellId].entropy   = fastLog2(sum) - (logSum / sum + EPSILONE);
                    }
                }

                private static float fastLog2(float value)
                {
                    const float invLog2 = 1.442695f; // 1 / log(2)
                    return (float)(Mathf.Log(value + EPSILONE) * invLog2);
                }
            }

            [BurstCompile]
            private struct FindMinEntropyCellPartialJob : IJobParallelForBatch
            {
                public Grid                             grid;
                public LevelData.Layer                  layer;

                // Output
                public NativeQueue<int>.ParallelWriter  minEntropy;
 
                public void Execute(int startIndex, int count)
                {
                    int cellId  = -1;
                    float min   = float.MaxValue;

                    for (var i = startIndex; i < startIndex + count; i++)
                    {
                        var cell = this.grid[this.layer, i];

                        if(!cell.isEmpty && !cell.isCollapsed && cell.entropy < min)
                        {
                            cellId = i;
                            min = cell.entropy;
                        }
                    }

                    if(cellId >= 0) { this.minEntropy.Enqueue(cellId); }
                }
            }
 
            [BurstCompile]
            private struct FindMinEntropyCellJob : IJob
            {
                // input
                public NativeQueue<int>     minEntropies;

                public Grid                 grid;
                public LevelData.Layer      layer;

                // Output
                public NativeReference<int> cellId;
 
                public void Execute()
                {
                    int cellId  = -1;
                    float min   = float.MaxValue;

                    while (this.minEntropies.TryDequeue(out var id))
                    {
                        var cell = this.grid[this.layer, id];
                        if(cell.entropy < min)
                        {
                            cellId = id;
                            min = cell.entropy;
                        }
                        // note: this if-else clause is actual imperial to ensure determenistic generation behaviour. Since the partial sort jobs
                        // can finish in any order the minimum cell ids might be also mixed up.
                        else if(cell.entropy == min && id < cellId)
                        {
                            cellId = id;
                            min = cell.entropy;
                        }
                    }

                    this.cellId.Value = cellId;
                }
            }

            private struct CollapseCellJob : IJob
            {
                [NativeDisableContainerSafetyRestriction]
                public Grid                             grid;
                public LevelData.Layer                  layer;

                [NativeDisableContainerSafetyRestriction]
                public GridCellModuleWeightsUndoBuffer  weightsBuffer;

                public float                            rng;

                [ReadOnly]
                public NativeReference<int>             cellId;

                public void Execute()
                {
                    var cellId                                  = this.cellId.Value;
                    if(cellId < 0) { return; }

                    var weights                                 = this.weightsBuffer.current;
                    var cellModuleWeights                       = weights.cellWeights(cellId);
                    var pickedModuleId                          = this.pickRandomModule(cellModuleWeights, this.rng * cellModuleWeights.Sum());

                    cellModuleWeights[pickedModuleId]           = 0.0f;

                    this.grid[this.layer, cellId].module      = pickedModuleId;
                    this.grid[this.layer, cellId].isCollapsed   = true;
                    this.grid[this.layer, cellId].entropy       = 0.0f;

                    this.weightsBuffer.apply();
                }

                private int pickRandomModule([ReadOnly]NativeSlice<float> distribution, float rng)
                {
                    float cumsum = 0.0f;
                    for(int moduleId = 0; moduleId < distribution.Length; moduleId++)
                    {
                        // zero values must be ignored
                        if(distribution[moduleId] <= EPSILONE) { continue; }

                        cumsum += distribution[moduleId];
                        if(cumsum >= rng) { return moduleId; }
                    }

                    return -1;
                }
            }

            private struct PropagateContraintsJob : IJob
            {
                [ReadOnly]
                public ModuleConstraints                constraints;

                [ReadOnly]
                public NativeReference<int>             collapsedCellId;

                public NativeReference<StateWFC>        state;

                public Grid                             grid;
                public LevelData.Layer                  layer;

                [NativeDisableContainerSafetyRestriction]
                public GridCellModuleWeightsUndoBuffer  weightsBuffer;

                public NativeArray<int>                 stack;

                private int                             stackPtr;

                /// <summary>
                /// Update cellId's cell possible modules that can be on the "side" of "modules".
                /// </summary>
                /// <param name="cellId"></param>
                /// <param name="modules"></param>
                /// <param name="side"></param>
                private void propagate(int cellId, in LevelData.Module[] modules, in ModuleConstraints.Side side)
                {
                    var cell = this.grid[this.layer, cellId];
                    if(!cell.isCollapsed)
                    {
                        //Debug.Log($"Propagating to {side.ToString()} cell [ID: {cellId}]: {String.Join(",", modules)}");

                        var weights = this.weightsBuffer.current.cellWeights(cellId);
                        var w1 = 0.0f;
                        var w2 = 0.0f;

                        for(int moduleB = 0; moduleB < weights.Length; moduleB++)
                        {
                            if(weights[moduleB] <= EPSILONE) { continue; }

                            ulong allowed = 0;
                            foreach(int moduleA in modules)
                            {
                                allowed |= this.constraints.isSet(moduleB, side, moduleA);
                            }

                            w1 += weights[moduleB];
                            weights[moduleB] *= allowed;
                            w2 += weights[moduleB];
                        }

                        //Debug.Log($"Remaining options {side.ToString()} cell {cellId}: {String.Join(",", weights.Select((w, moduleId) => w > Step3.EPSILONE ? moduleId : -1).Where(id => id != -1))}");

                        if((w1 - w2) > EPSILONE)
                        {
                            this.stack[stackPtr++] = cellId;
                        }
                    }
                }

                public void Execute()
                {
                    this.stackPtr               = 0;

                    // if modeules empty -> no solution!
                    if(this.collapsedCellId.Value < 0 || this.state.Value.hasFailed())
                    {
                        this.state.Value.fail();
                        return;
                    }

                    this.stack[stackPtr++]      = this.collapsedCellId.Value;

                    //Debug.Log($"CellId: {this.collapsedCellId.Value} collapsed to {this.grid[this.layer, this.collapsedCellId.Value].moduleId}");
                    while(stackPtr > 0)
                    {
                        var cellId              = this.stack[--stackPtr];

                        var cell                = this.grid[this.layer, cellId];
                        var modules             = cell.module != LevelData.Module.INVALID
                            ? new LevelData.Module[] { cell.module }
                            : this.weightsBuffer.current.cellWeights(cellId)
                                .Select((weight, moduleId) => weight > 0.0f ? (LevelData.Module)moduleId : LevelData.Module.INVALID)
                                .Where(moduleId => moduleId != LevelData.Module.INVALID).ToArray();

              
                        // if modeules empty -> no solution!
                        if(modules.Length == 0)
                        {
                            //Debug.Log($"failed: {cellId}");
                            this.state.Value.fail();
                            return;
                        }

                        // LEFT
                        var cellIdLeft = cellId - 1;
                        if((cellIdLeft % this.grid.width) != (this.grid.width - 1) && cellIdLeft >= 0)
                        {
                            this.propagate(cellIdLeft, modules, ModuleConstraints.Side.Left);
                        }

                        // RIGHT
                        var cellIdRight = cellId + 1;
                        if((cellIdRight % this.grid.width) != 0)
                        {
                            this.propagate(cellIdRight, modules, ModuleConstraints.Side.Right);
                        }

                        // TOP
                        var cellIdTop = cellId - this.grid.width;
                        if(cellIdTop >= 0)
                        {
                            this.propagate(cellIdTop, modules, ModuleConstraints.Side.Top);
                        }

                        // BOTTOM
                        var cellIdBottom = cellId + this.grid.width;
                        if(cellIdBottom < this.grid.size)
                        {
                            this.propagate(cellIdBottom, modules, ModuleConstraints.Side.Bottom);
                        }
                    }
                }

            }

            private struct UpdateMapChunkDataJob : IJob
            {
                [ReadOnly]
                public NativeReference<int>             cellId;

                public Grid                             grid;
                public LevelData.Layer                  layer;

                public void Execute()
                {
                    if(this.cellId.Value != -1) { this.grid[this.layer, this.cellId.Value].apply(); }
                }
            }

            [BurstCompile]
            private struct AllCellsCollapsedJob : IJob
            {
                public Grid                         grid;
                public LevelData.Layer              layer;

                public NativeReference<StateWFC>  state;

                [BurstCompile]
                public void Execute()
                {
                    this.state.Value.allCollapsed(true);
                    for(int i = 0; i < (this.grid.size); i++)
                    {
                        if(!this.grid[this.layer, i].isEmpty && !this.grid[this.layer, i].isCollapsed)
                        {
                            this.state.Value.allCollapsed(false);
                            break;
                        }
                    }
                }
            }

            [BurstCompile]
            private struct HasSolutionJob : IJob
            {
                public Grid                         grid;
                public LevelData.Layer              layer;

                public NativeReference<StateWFC>    state;

                [BurstCompile]
                public void Execute()
                {
                    this.state.Value.hasSolution(true);
                    for(int i = 0; i < (this.grid.size); i++)
                    {
                        if(!this.grid[this.layer, i].isEmpty && this.grid[this.layer, i].module == -1)
                        {
                            this.state.Value.hasSolution(false);
                            break;
                        }
                    }
                }
            }



            private ModuleConstraints                       constraints;
            private ModuleConstraints                       passTwoConstraints;

            private NativeReference<StateWFC>               state;
                                                    
            private Grid                                    grid;

            private NativeArray<ModuleMeta>                 modules;
            private GridCellModuleWeightsUndoBuffer         weightsBuffer;
                                                    
            private NativeQueue<int>                        minEntropyQueue;
            private NativeReference<int>                    minEntropyCell;
            private NativeArray<int>                        stack;

            private NativeHashSet<Vector2Int>               processedMapChunks;

            public override IEnumerator<State> execute(Generator.Context context)
            {
                // initialize module contraints
                this.constraints                            = new ModuleConstraints(this.modules.AsReadOnly());
                this.passTwoConstraints                     = new ModuleConstraints(this.modules.AsReadOnly());

                var constraintsJob                          = new InitializeConstraintsJob
                {
                    similarityThreshold                     = this.settings.moduleSimilarityThreshold,
                    similarityPercentile                    = this.settings.moduleSimilarityPercentile,
                    constraints                             = this.constraints,
                    modules                                 = this.modules
                };

                var dependsOn = context.schedule(constraintsJob, this.modules.Length, this.modules.Length);

                for(int chunkId = 0; chunkId < context.level.numChunks; chunkId++)
                {
                    var chunkInfo = context.level.getChunk(chunkId);
                    if(this.processedMapChunks.Contains(chunkInfo.bounds.position)) { continue; }
                    this.processedMapChunks.Add(chunkInfo.bounds.position);



                    this.state.Value.reset();

                    var initGrid = this.initializeGrid(chunkId, context);
                    while(initGrid.MoveNext()) { yield return initGrid.Current; }

                    this.weightsBuffer                      = new GridCellModuleWeightsUndoBuffer(this.grid, this.modules, this.settings.numBacktrackingSteps);
                    this.stack                              = new NativeArray<int>(this.grid.size * this.grid.size, Allocator.Persistent);

                    // 1st pass - generate floor
                    var passOne = this.passOne(context, this.grid, dependsOn);
                    while(passOne.MoveNext()) { yield return passOne.Current; }

                    // 2nd pass - generate obstructables
                    var passTwo = this.passTwo(context, this.grid, dependsOn);
                    while(passTwo.MoveNext()) { yield return passTwo.Current; }

                    if(this.state.Value.hasSolution())
                        Debug.Log($"Map chunk {chunkId} generation successfull. [Attepts: {this.state.Value.numFails() + 1}]");
                    else
                        Debug.LogWarning($"Map chunk {chunkId} generation failed.");

                    this.weightsBuffer.Dispose();
                    this.grid.Dispose();
                    this.stack.Dispose();
                }
            }

            private IEnumerator<State> initializeGrid(int chunkId, Generator.Context context)
            {
                var chunk                               = context.level.getChunk(chunkId);
        
                var chunkLeft                           = context.level.getChunk(chunk.bounds.position + new Vector2Int(-chunk.bounds.width, 0));
                var chunkRight                          = context.level.getChunk(chunk.bounds.position + new Vector2Int(chunk.bounds.width, 0));
                var chunkTop                            = context.level.getChunk(chunk.bounds.position + new Vector2Int(0, -chunk.bounds.height));
                var chunkBottom                         = context.level.getChunk(chunk.bounds.position + new Vector2Int(0, chunk.bounds.height));

                var chunkTopLeft                        = context.level.getChunk(chunk.bounds.position + new Vector2Int(-chunk.bounds.width, -chunk.bounds.height));
                var chunkTopRight                       = context.level.getChunk(chunk.bounds.position + new Vector2Int(chunk.bounds.width, -chunk.bounds.height));
                var chunkBottomLeft                     = context.level.getChunk(chunk.bounds.position + new Vector2Int(-chunk.bounds.width, chunk.bounds.height));
                var chunkBottomRight                    = context.level.getChunk(chunk.bounds.position + new Vector2Int(chunk.bounds.width, chunk.bounds.height));

                // note: this will ensure we only use already generated neighbouring cells
                if(chunkLeft.HasValue && chunkLeft.Value.id > chunk.id) { chunkLeft = null; }
                if(chunkRight.HasValue && chunkRight.Value.id > chunk.id) { chunkRight = null; }
                if(chunkTop.HasValue && chunkTop.Value.id > chunk.id) { chunkTop = null; }
                if(chunkBottom.HasValue && chunkBottom.Value.id > chunk.id) { chunkBottom = null; }
                if(chunkTopLeft.HasValue && chunkTopLeft.Value.id > chunk.id) { chunkTopLeft = null; }
                if(chunkTopRight.HasValue && chunkTopRight.Value.id > chunk.id) { chunkTopRight = null; }
                if(chunkBottomLeft.HasValue && chunkBottomLeft.Value.id > chunk.id) { chunkBottomLeft = null; }
                if(chunkBottomRight.HasValue && chunkBottomRight.Value.id > chunk.id) { chunkBottomRight = null; }

                var gridWidth                           = chunk.bounds.width;
                var gridHeight                          = chunk.bounds.height;

                if(chunkLeft.HasValue)                  { gridWidth++; }
                if(chunkRight.HasValue)                 { gridWidth++; }
                if(chunkTop.HasValue)                   { gridHeight++; }
                if(chunkBottom.HasValue)                { gridHeight++; }


                this.grid                                   = Grid.Create(gridWidth, gridHeight);

                foreach(var layer in new[] { LevelData.Layer.Floor, LevelData.Layer.Obstructable })
                {
                    this.grid.addLayer(layer);

                    var initializeGridJob                   = new InitializeGridJob
                    {
                        grid                                = this.grid,
                        layer                               = layer,
                        chunk                               = chunk,

                        chunkLeft                           = chunkLeft.HasValue
                            // get slice of the bottom border of the top neighbour map chunk
                            ? chunkLeft.Value.data
                            : chunk.data.Slice(0, 0),
                        chunkRight                          = chunkRight.HasValue
                            // get slice of the left border of the right neighbour map chunk
                            ? chunkRight.Value.data
                            : chunk.data.Slice(0, 0),
                        chunkTop                            = chunkTop.HasValue
                            // get slice of the bottom border of the top neighbour map chunk
                            ? chunkTop.Value.data.Slice(chunkTop.Value.data.Length - chunkTop.Value.bounds.width, chunkTop.Value.bounds.width)
                            : chunk.data.Slice(0, 0),
                        chunkBottom                         = chunkBottom.HasValue
                            // get slice of the top border of the bottom neighbour map chunk
                            ? chunkBottom.Value.data.Slice(0, chunkBottom.Value.bounds.width)
                            : chunk.data.Slice(0, 0),

                        chunkTopLeft                        = chunkTopLeft.HasValue
                            ? chunkTopLeft.Value.data.Slice(chunkTopLeft.Value.data.Length - 1, 1)
                            : chunk.data.Slice(0, 0),
                        chunkTopRight                       = chunkTopRight.HasValue
                            ? chunkTopRight.Value.data.Slice(chunkTopRight.Value.data.Length - chunkTopRight.Value.bounds.width, 1)
                            : chunk.data.Slice(0, 0),
                        chunkBottomLeft                     = chunkBottomLeft.HasValue
                            ? chunkBottomLeft.Value.data.Slice(chunkBottomLeft.Value.bounds.width - 1, 1)
                            : chunk.data.Slice(0, 0),
                        chunkBottomRight                    = chunkBottomRight.HasValue
                            ? chunkBottomRight.Value.data.Slice(0, 1)
                            : chunk.data.Slice(0, 0),
                    };

                    context.scheduleBatch(initializeGridJob, this.grid.size, this.grid.width);
                    yield return State.Default;
                }
            }

            private IEnumerator<State> passOne(Generator.Context context, Grid grid, JobHandle dependsOn)
            {
                this.state.Value.hasSolution(false);

                while(!this.state.Value.hasSolution() && (this.settings.numAttemptsToSolveChunk == 0 || this.state.Value.numFails() < this.settings.numAttemptsToSolveChunk))
                {
                    // reset all grid cells
                    grid.reset(LevelData.Layer.Floor);
            
                    // set initial weights acording to current map chunk mask
                    var initializeWeightsJob            = new InitializePassOneWeightsJob
                    {
                        grid                            = this.grid,
                        layer                           = LevelData.Layer.Floor,
                        weightsBuffer                   = this.weightsBuffer,
                        modules                         = this.modules,
                    };
            
                    dependsOn = context.scheduleBatch(initializeWeightsJob, this.grid.size, this.grid.width, dependsOn);
            
                    var WFC = this.runWFC(context, grid, LevelData.Layer.Floor, dependsOn);
                    while(WFC.MoveNext()) { yield return WFC.Current; }

                    if(this.state.Value.hasFailed() && !this.state.Value.isRecoverable()) { break; }
                }
            }

            private IEnumerator<State> passTwo(Generator.Context context, Grid grid, JobHandle dependsOn)
            {
                this.state.Value.hasSolution(false);

                while(!this.state.Value.hasSolution() && (this.settings.numAttemptsToSolveChunk == 0 || this.state.Value.numFails() < this.settings.numAttemptsToSolveChunk))
                {
                    // reset all grid cells
                    grid.reset(LevelData.Layer.Obstructable);

                    // set initial weights acording to current map chunk mask
                    var initializeWeightsJob            = new InitializePassTwoWeightsJob
                    {
                        grid                            = grid,
                        layer                           = LevelData.Layer.Obstructable,
                        mapChunkObstructivness          = this.settings.chunkObstructivness,          
                        weightsBuffer                   = this.weightsBuffer,
                        modules                         = this.modules,
                    };

                    dependsOn = context.scheduleBatch(initializeWeightsJob, this.grid.size, this.grid.width, dependsOn);

                    var WFC = this.runWFC(context, grid, LevelData.Layer.Obstructable, dependsOn);
                    while(WFC.MoveNext()) { yield return WFC.Current; }

                    var postJob = new PassTwoPostJob
                    {
                        grid                            = grid,
                        layer                           = LevelData.Layer.Obstructable,
                        modules                         = this.modules
                    };

                    context.scheduleBatch(postJob, this.grid.size, this.grid.width, dependsOn);
                    yield return State.Default;

                    if(this.state.Value.hasFailed() && !this.state.Value.isRecoverable()) { break; }
                }
            }

            private IEnumerator<State> runWFC(Generator.Context context, Grid grid, LevelData.Layer layer, JobHandle dependsOn)
            {
                this.state.Value.hasFailed(false);
                this.state.Value.allCollapsed(false);

                // pre-propagate module weights from pre-set (collapsed) cells
                for(var cellId = 0; cellId < grid.size; cellId++)
                {
                    if(this.state.Value.hasFailed()) { break; }

                    var cell = grid[layer, cellId];
                    if(!cell.isCollapsed || cell.isEmpty) { continue; }

                    this.minEntropyCell.Value       = cellId;
                    var propagateJob                = new PropagateContraintsJob
                    {
                        grid                        = grid,
                        layer                       = layer,
                        collapsedCellId             = this.minEntropyCell,
                        weightsBuffer               = this.weightsBuffer,
                        constraints                 = this.constraints,
                        stack                       = this.stack,
                        state                       = this.state
                    };

                    var updateMapChunkDataJob       = new UpdateMapChunkDataJob
                    {
                        cellId                      = this.minEntropyCell,
                        grid                        = grid,
                        layer                       = layer,
                    };

                    dependsOn = context.schedule(propagateJob, dependsOn);
                    dependsOn = context.schedule(updateMapChunkDataJob, dependsOn);

                    yield return State.Default;
                }

                if(this.state.Value.hasFailed())
                {
                    this.state.Value.isRecoverable(false);
                    yield break;
                }

                while(!this.state.Value.hasFailed() && !this.state.Value.allCollapsed())
                {
                    dependsOn = this.doWaveFunctionCollapse(context, layer, dependsOn);
                    // wait for WFC iteration result
                    yield return State.Default;

                    // if WFC run into an unsolvable state, try to recover by restaring WFC from previous step
                    if(this.state.Value.hasFailed() && this.weightsBuffer.canUndo)
                    {
                        this.weightsBuffer.undo();
                        this.state.Value.hasFailed(false);
                    }
                    else
                    {
                        var updateMapChunkDataJob       = new UpdateMapChunkDataJob
                        {
                            cellId                      = this.minEntropyCell,
                            grid                        = grid,
                            layer                       = layer,
                        };
                        dependsOn = context.schedule(updateMapChunkDataJob, dependsOn);

                        dependsOn = this.checkAllCellsCollapsed(context, layer, dependsOn);

                        // hand over controll to pipeline executor
                        yield return State.Default;
                    }
                }

                this.checkSolution(context, layer, dependsOn);

                yield return State.Default;
            }

            private JobHandle doWaveFunctionCollapse(Generator.Context context, LevelData.Layer layer, JobHandle dependsOn)
            {
                var calculateEntropiesJob       = new ComputeEntropiesJob
                {
                    grid                        = this.grid,
                    layer                       = layer,
                    weightsBuffer               = this.weightsBuffer,
                };

                dependsOn = context.scheduleBatch(calculateEntropiesJob, this.grid.size, this.grid.width, dependsOn);

                // find min entropy cell id
                {
                    var findPartialJob          = new FindMinEntropyCellPartialJob
                    {
                        grid                    = this.grid,
                        layer                   = layer,
                        minEntropy              = this.minEntropyQueue.AsParallelWriter(),
                    };

                    dependsOn = context.scheduleBatch(findPartialJob, this.grid.size, this.grid.width, dependsOn);

                    var findMinimumJob          = new FindMinEntropyCellJob
                    {
                        grid                    = this.grid,
                        layer                   = layer,
                        minEntropies            = this.minEntropyQueue,
                        cellId                  = this.minEntropyCell
                    };

                    dependsOn = context.schedule(findMinimumJob, dependsOn);
                }

                var collapseCellJob             = new CollapseCellJob
                {
                    grid                        = this.grid,
                    layer                       = layer,
                    weightsBuffer               = this.weightsBuffer,
                    cellId                      = this.minEntropyCell,
                    rng                         = (float)context.random.NextDouble(),
                };

                dependsOn = context.schedule(collapseCellJob, dependsOn);

                var propagateJob = new PropagateContraintsJob
                {
                    grid                        = this.grid,
                    layer                       = layer,
                    collapsedCellId             = this.minEntropyCell,
                    weightsBuffer               = this.weightsBuffer,
                    constraints                 = this.constraints,
                    stack                       = this.stack,
                    state                       = this.state
                };
                return context.schedule(propagateJob, dependsOn);
            }

            private JobHandle checkAllCellsCollapsed(Generator.Context context, LevelData.Layer layer, JobHandle dependsOn)
            {
                // check if all cells are collapsed now
                var checkAllCollapsedJob        = new AllCellsCollapsedJob
                {
                    grid                        = this.grid,
                    layer                       = layer,
                    state                       = this.state
                };

                return context.schedule(checkAllCollapsedJob, dependsOn);
            }

            private void checkSolution(Generator.Context context, LevelData.Layer layer, JobHandle dependsOn)
            {
                // if any cell has an invalid module id, hasSolution value will be set false
                var hasSolutionJob              = new HasSolutionJob
                {
                    grid                        = this.grid,
                    layer                       = layer,
                    state                       = this.state
                };

                context.schedule(hasSolutionJob);
            }

            private string dumpContraints(in ModuleConstraints contraints, int moduleId = -1)
            {
                int PAD = $"{this.modules.Length}".Length;
        
                string buffer = "";
                for(int i = 0; i < this.modules.Length; i++)
                {
                    buffer += $"ModuleId: {i} = {this.modules[i].name.ToString().PadLeft(PAD, '0')}\n";
                }

                buffer += "M |";
                for(int moduleA = moduleId == -1 ? 0 : moduleId; moduleA < (moduleId == -1 ? this.modules.Length : moduleId + 1); moduleA++)
                {
                    for(int moduleB = 0; moduleB < this.modules.Length; moduleB++)
                    {
                        buffer += $" {moduleB.ToString().PadLeft(PAD, '0')}";
                    }
                    buffer += " |";
                }

                buffer += "\n";
                for(int side = 0; side < 4; side++)
                {
                    buffer += $"{((ModuleConstraints.Side)side).ToString()[0]} |";
                    for(int moduleA = moduleId == -1 ? 0 : moduleId; moduleA < (moduleId == -1 ? this.modules.Length : moduleId + 1); moduleA++)
                    {
                        for(int moduleB = 0; moduleB < this.modules.Length; moduleB++)
                        {
                            buffer += $" {contraints.isSet(moduleA, (ModuleConstraints.Side)side, moduleB).ToString().PadLeft(PAD, '0')}";
                        }

                        buffer += " |";
                    }
                    buffer += "\n";
                }

                return buffer;
            }

            public override void initialize(Generator.Context context)
            {
                this.state                      = new NativeReference<StateWFC>(StateWFC.Create(), Allocator.Persistent);
                this.modules                    = new NativeArray<ModuleMeta>(context.settings.modules.Select(m => (ModuleMeta)m).ToArray(), Allocator.Persistent);
                this.minEntropyQueue            = new NativeQueue<int>(Allocator.Persistent);
                this.minEntropyCell             = new NativeReference<int>(Allocator.Persistent);
                this.processedMapChunks         = new NativeHashSet<Vector2Int>(context.level.numChunks, Allocator.Persistent);
            }

            public override void release(Generator.Context context)
            {
                if(this.modules.IsCreated)
                {
                    foreach(var module in this.modules) { module.Dispose(); }
                    this.modules.Dispose();
                }

                if(this.processedMapChunks.IsCreated) this.processedMapChunks.Dispose();
                if(this.weightsBuffer.IsCreated) this.weightsBuffer.Dispose();
                if(this.grid.IsCreated) this.grid.Dispose();
                if(this.constraints.IsCreated) this.constraints.Dispose();
                if(this.passTwoConstraints.IsCreated) this.passTwoConstraints.Dispose();
                if(this.minEntropyQueue.IsCreated) this.minEntropyQueue.Dispose();
                if(this.minEntropyCell.IsCreated) this.minEntropyCell.Dispose();
                if(this.stack.IsCreated) this.stack.Dispose();

                if(this.state.IsCreated) this.state.Dispose();
            }
        }
    }
}
