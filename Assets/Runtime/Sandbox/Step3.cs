using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;
using Unity.VisualScripting.Antlr3.Runtime;
using Unity.VisualScripting;
using UnityEngine.Tilemaps;

public partial struct MapGenData
{
}


public class ColorComparer
{
    public static float CalculateDeltaE(in Color c1, in Color c2)
    {
        //return 1f - (Vector3.Distance(new Vector3(c1.r, c1.g, c1.b), new Vector3(c2.r, c2.g, c2.b)) / Mathf.Sqrt(3f));

        return CIEDE2000(RGBToLab(c1), RGBToLab(c2));
    }

    // Helper method to convert RGB to Lab color space using SIMD
    private static float3 RGBToLab(in Color color)
    {
        float3 c = new float3(color.r, color.g, color.b);
        float3 xyz = new float3(
            math.dot(new float3(0.4124564f, 0.3575761f, 0.1804375f), c),
            math.dot(new float3(0.2126729f, 0.7151522f, 0.0721750f), c),
            math.dot(new float3(0.0193339f, 0.1191920f, 0.9503041f), c)
        ) / 255f;

        float3 fxyz = math.select(math.pow(xyz / 0.950456f, 1.0f / 3.0f), (xyz * 7.787f) + (16f / 116f), xyz > 0.008856f);
        float3 lab = new float3((116f * fxyz.y) - 16f, 500f * (fxyz.x - fxyz.y), 200f * (fxyz.y - fxyz.z));

        return lab;
    }

    private static float CIEDE2000(float3 lab1, float3 lab2)
    {
        float kL = 1f, kC = 1f, kH = 1f; // Set weighting factors
        float deg2rad = math.PI / 180f;

        float C1 = math.length(lab1.yz);
        float C2 = math.length(lab2.yz);
        float barC = (C1 + C2) * 0.5f;

        float G = 0.5f * (1f - math.sqrt(math.pow(barC, 7f) / (math.pow(barC, 7f) + math.pow(25f, 7f))));

        float2 a1Prime = (1f + G) * lab1.yz;
        float2 a2Prime = (1f + G) * lab2.yz;

        float C1Prime = math.length(a1Prime);
        float C2Prime = math.length(a2Prime);

        float2 h1Prime = math.atan2(lab1.z, a1Prime);
        float2 h2Prime = math.atan2(lab2.z, a2Prime);

        float deltaL = lab2.x - lab1.x;
        float deltaC = C2Prime - C1Prime;

        float2 hBarPrime = math.abs(h1Prime - h2Prime);
        float deltaH = math.csum(2f * math.sqrt(C1Prime * C2Prime) * math.sin((hBarPrime * 0.5f) * deg2rad));

        float LBarPrime = (lab1.x + lab2.x) * 0.5f;
        float CBarPrime = (C1Prime + C2Prime) * 0.5f;

        float2 hBarPrimeAbs = math.abs(h1Prime - h2Prime);
        float2 hBarPrimeAbs2 = math.select(hBarPrimeAbs, 360f - hBarPrimeAbs, hBarPrimeAbs > 180f);

        float HBarPrime = math.csum(math.select((h1Prime + h2Prime) * 0.5f, (h1Prime + h2Prime) * 0.5f + 180f, hBarPrimeAbs2 <= hBarPrimeAbs));

        float T = 1f - 0.17f * math.cos((HBarPrime - 30f) * deg2rad) + 0.24f * math.cos((2f * HBarPrime) * deg2rad) + 0.32f * math.cos((3f * HBarPrime + 6f) * deg2rad) - 0.20f * math.cos((4.5f * HBarPrime - 63f) * deg2rad);

        float dL = deltaL / kL;
        float dC = deltaC / kC;
        float dH = deltaH / kH;

        float deltaE = math.sqrt(math.pow(dL, 2f) + math.pow(dC, 2f) + math.pow(dH, 2f) + T * (dC * dH));

        return deltaE;
    }
}


public class Step3 : MapGenStep<Step3Settings>
{
    public const float              EPSILONE = 1e-6f;

    public struct ModuleMeta : IDisposable
    {
        [ReadOnly]
        public UnsafeList<Color>            pixels;

        [ReadOnly]
        public Vector2Int                   textureSize;

        [ReadOnly]
        public float                        weight;

        [ReadOnly]
        public UnsafeText                   name;

        [ReadOnly]
        public TileConstructionType         constructionType;

        public bool IsCreated { get { return this.pixels.IsCreated || this.name.IsCreated; } }
        public void Dispose()
        {
            if(this.pixels.IsCreated) { this.pixels.Dispose(); }
            if(this.name.IsCreated) { this.name.Dispose(); }
        }

        public static implicit operator ModuleMeta(Module module)
        {
            unsafe
            {
                var x       = Mathf.FloorToInt(module.sprite.textureRect.x);
                var y       = Mathf.FloorToInt(module.sprite.textureRect.y);
                var width   = Mathf.FloorToInt(module.sprite.textureRect.width);
                var height  = Mathf.FloorToInt(module.sprite.textureRect.height);
                var pixels  = new UnsafeList<Color>(width * height, Allocator.Persistent);

                foreach(var pixel in module.sprite.texture.GetPixels(x, y, width, height))
                {
                    pixels.AddNoResize(pixel);
                }

                //var t = new Texture2D(width, 1);
                ////t.SetPixels(module.sprite.texture.GetPixels(x, y, width, height));
                ////for(int py = 0; py < height; py++)
                ////{
                ////    var r  = pixels[py * width +  (width - 1)]; // moduleA's right border
                ////    var l  = pixels[py * width];                // moduleB's left border

                ////    //t.SetPixel(0, py, l);
                ////    t.SetPixel(0, py, r);
                ////}
                //var bottomBorderOffset  = (height - 1) * width;

                //for(int px = 0; px < width; px++)
                //{
                //    var tp  = pixels[px];                                         // moduleA's top border
                //    var b   = pixels[px + bottomBorderOffset];                    // moduleB's bottom border

                //    //t.SetPixel(px, 0, tp);
                //    t.SetPixel(px, 0, b);
                //}
                //t.Apply();

                //byte[] bytes = t.EncodeToPNG();
                //var dirPath = Application.dataPath + "/RenderOutput/";
                //if (!System.IO.Directory.Exists(dirPath))
                //{
                //    System.IO.Directory.CreateDirectory(dirPath);
                //}
                //System.IO.File.WriteAllBytes(dirPath + $"{module.name}-tb.png", bytes);

                var name = new UnsafeText(module.name.Length, Allocator.Persistent);
                name.CopyFrom(module.name);

                return new ModuleMeta {
                    pixels              = pixels,
                    textureSize         = new Vector2Int(width, height),
                    weight              = module.weight,
                    name                = name,
                    constructionType    = module.constructionType
                };
            }
        }
    }

    [BurstCompile]
    public struct CellMeta : IComparable<CellMeta>
    {
        public enum CellMetaStates
        {
            Collapsed = 0,
            Propagated
        }

        public int          moduleId;
        public float        entropy;

        public BitField32   states;

        

        public static CellMeta Create()
        {
            return new CellMeta
            {
                isCollapsed     = false,
                entropy         = 0.0f,
                moduleId        = -1,
                propagated      = false,
                states          = new BitField32(0)
            };
        }

        public bool isCollapsed { get { return this.states.IsSet((int)CellMetaStates.Collapsed); } set { this.states.SetBits((int)CellMetaStates.Collapsed, value); } }
        public bool propagated { get { return this.states.IsSet((int)CellMetaStates.Propagated); } set { this.states.SetBits((int)CellMetaStates.Propagated, value); } }

        public int CompareTo(CellMeta other)
        {
            if(this.isCollapsed && other.isCollapsed) { return 0; }
            if(this.isCollapsed) { return 1; }
            if(other.isCollapsed) { return -1; }

            return this.entropy.CompareTo(other.entropy);
        }
    }

    [BurstCompile]
    public struct ModuleConstraints : IDisposable
    {
        public enum Side { Left = 0, Right, Top, Bottom }

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
        public void set(int moduleA, Side side, int moduleB, bool allow = true)
        {
            var moduleARowStartIndex = moduleA * this.numModules;
            var moduleBRowStartIndex = moduleB * this.numModules;

            switch(side)
            {
                case Side.Left:
                {
                    this.constraints.Set((int)Side.Left   * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                    this.constraints.Set((int)Side.Right  * this.rowLength + moduleARowStartIndex + moduleB, allow);
                    break;
                }

                case Side.Right:
                {
                    this.constraints.Set((int)Side.Right  * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                    this.constraints.Set((int)Side.Left   * this.rowLength + moduleARowStartIndex + moduleB, allow);
                    break;
                }

                case Side.Top:
                {
                    this.constraints.Set((int)Side.Top    * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                    this.constraints.Set((int)Side.Bottom * this.rowLength + moduleARowStartIndex + moduleB, allow);
                    break;
                }

                case Side.Bottom:
                {
                    this.constraints.Set((int)Side.Bottom * this.rowLength + moduleBRowStartIndex + moduleA, allow);
                    this.constraints.Set((int)Side.Top    * this.rowLength + moduleARowStartIndex + moduleB, allow);
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
        public ulong isSet(int moduleA, Side side, int moduleB)
        {
            return this.constraints.GetBits(((int)side * this.rowLength) + (moduleB * this.numModules) + moduleA);
        }
    }


    [BurstCompile]
    private struct InitializeVisualConstraintsJob : IJobParallelFor
    {
        [NativeDisableParallelForRestriction] 
        public ModuleConstraints        constraints;

        [ReadOnly]
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
        }

        private void check(int moduleA, ModuleConstraints.Side side, int moduleB)
        {
            var mA          = this.modules[moduleA];
            var mB          = this.modules[moduleB];

            var similarity  = 0.0f;

            switch(side)
            {
                case ModuleConstraints.Side.Left:
                {
                    var rightBorderOffset   = (mA.textureSize.x - 1);
                    var Y                   = Mathf.Min(mA.textureSize.y, mB.textureSize.y);

                    for(int py = 0; py < Y; py++)
                    {
                        var pixelA  = mA.pixels[py * mA.textureSize.x + rightBorderOffset]; // moduleA's right border
                        var pixelB  = mB.pixels[py * mB.textureSize.x];                     // moduleB's left border
                        similarity  += ColorComparer.CalculateDeltaE(pixelA, pixelB);
                    }

                    similarity /= Y;
                    break;
                }

                case ModuleConstraints.Side.Right:
                {
                    var rightBorderOffset   = (mB.textureSize.x - 1);
                    var Y                   = Mathf.Min(mA.textureSize.y, mB.textureSize.y);

                    for(int py = 0; py < Y; py++)
                    {
                        var pixelA  = mA.pixels[py * mA.textureSize.x];                     // moduleA's left border
                        var pixelB  = mB.pixels[py * mB.textureSize.x + rightBorderOffset]; // moduleB's right border
                        similarity  += ColorComparer.CalculateDeltaE(pixelA, pixelB);

                    }

                    similarity /= Y;
                    break;
                }

                case ModuleConstraints.Side.Top:
                {
                    var bottomBorderOffset  = (mA.textureSize.y - 1) * mA.textureSize.x;
                    var X                   = Mathf.Min(mA.textureSize.x, mB.textureSize.x);

                    for(int px = 0; px < X; px++)
                    {
                        var pixelA  = mA.pixels[px + bottomBorderOffset];   // moduleA's bottom border
                        var pixelB  = mB.pixels[px];                        // moduleB's top border
                        similarity  += ColorComparer.CalculateDeltaE(pixelA, pixelB);
                    }

                    similarity /= X;
                    break;
                }

                case ModuleConstraints.Side.Bottom:
                {
                    var bottomBorderOffset  = (mB.textureSize.y - 1) * mB.textureSize.x;
                    var X                   = Mathf.Min(mA.textureSize.x, mB.textureSize.x);

                    for(int px = 0; px < X; px++)
                    {
                        var pixelA  = mA.pixels[px];                        // moduleA's top border
                        var pixelB  = mB.pixels[px + bottomBorderOffset];   // moduleB's bottom border
                        similarity  += ColorComparer.CalculateDeltaE(pixelA, pixelB);
                    }

                    similarity /= X;
                    break;
                }
            }

            //Debug.Log($"{moduleA}-{side.ToString()[0]}-{moduleB}: {similarity}");
            // note: lower CIEDE2000 values are more similar, values bellow one are considered hardly distigushable by the human eye
            this.constraints.set(moduleA, side, moduleB, similarity < 1.0f);
        }
    }
  
    [BurstCompile]
    private struct InitializeWeightsJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<ModuleMeta>          modules;

        [NativeDisableParallelForRestriction]
        public NativeArray<float>               weights;

        [ReadOnly]
        public NativeSlice<MapChunkData>    mapChunkData;

        public void Execute(int start, int count)
        {
            var weightChunkSize   = count * this.modules.Length;
            var weightChunkStart  = start * this.modules.Length;

            for(int i = weightChunkStart; i < weightChunkStart + weightChunkSize; i += this.modules.Length)
            {
                var tileId      = Mathf.FloorToInt(i / this.modules.Length);
                var tileData    = this.mapChunkData[tileId];

                for(int moduleId = 0; moduleId < this.modules.Length; moduleId++)
                {
                    var moduleConstructionType = this.modules[moduleId].constructionType;

                    switch(tileData.constructionType)
                    {
                        case TileConstructionType.Undefined:
                        {
                            this.weights[i + moduleId] = this.modules[moduleId].weight;
                            break;
                        }

                        default:
                        {
                            this.weights[i + moduleId] = moduleConstructionType == tileData.constructionType
                                ? this.modules[moduleId].weight
                                : 0.0f;

                            break;
                        }
                    }
                    
                }
            }
        }
    }

    [BurstCompile]
    private struct ComputeEntropiesJob : IJobParallelForBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeArray<CellMeta>    cellMetas;

        [ReadOnly]
        public NativeArray<float>       weights;

        public int                      numModules;

        public void Execute(int startIndex, int count)
        {
            for(int i = 0; i < count; i++)
            {
                var cellId  = startIndex + i;
                var cell = this.cellMetas[cellId];

                if(cell.isCollapsed) { continue; }

                var weights     = this.weights.Slice(cellId * numModules, numModules);

                //shannon_entropy_for_square = log(sum(weight)) - (sum(weight * log(weight)) / sum(weight))
                // note: We use EPSILONE to deal with possible zero weights, which would results in NaN values in the log
                float sum       = 0.0f;
                float logSum    = 0.0f;
                for(int w = 0; w < weights.Length; w++)
                {
                    sum         += weights[w];
                    logSum      += weights[w] * fastLog2(weights[w]);
                }

                // update cell
                cell.entropy            = fastLog2(sum) - (logSum / sum + Step3.EPSILONE);
                cell.propagated         = false;
                this.cellMetas[cellId]  = cell;
            }
        }

        private static float fastLog2(float value)
        {
            const float invLog2 = 1.442695f; // 1 / log(2)
            return (float)(Mathf.Log(value + Step3.EPSILONE) * invLog2);
        }
    }

    [BurstCompile]
    private struct FindMinEntropyCellPartialJob : IJobParallelForBatch
    {
        [ReadOnly]
        public NativeArray<CellMeta> cellMetas;
 
        // Output
        public NativeQueue<int>.ParallelWriter minEntropy;
 
        public void Execute(int startIndex, int count)
        {
            int cellId  = -1;
            float min   = float.MaxValue;

            for (var i = startIndex; i < Mathf.Min(startIndex + count, this.cellMetas.Length); i++)
            {
                var cell = this.cellMetas[i];
                if(!cell.isCollapsed && cell.entropy < min)
                {
                    cellId = i;
                    min = cell.entropy;
                }
            }

            if(cellId >= 0)
            {
                this.minEntropy.Enqueue(cellId);
            }
        }
    }
 
    [BurstCompile]
    private struct FindMinEntropyCellJob : IJob
    {
        // input
        public NativeQueue<int> minEntropies;

        [ReadOnly]
        public NativeArray<CellMeta> cellMetas;

        // Output
        public NativeReference<int> cellId;
 
        public void Execute()
        {
            int cellId  = -1;
            float min   = float.MaxValue;
 
            while (this.minEntropies.TryDequeue(out var id))
            {
                var cell = this.cellMetas[id];
                if(cell.entropy < min)
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
        public NativeArray<CellMeta>            cellMetas;

        [ReadOnly]
        public NativeArray<float>               weights;

        public float                            rng;
        public int                              numModules;

        [ReadOnly]
        public NativeReference<int>             cellId;

        [NativeDisableParallelForRestriction]
        public NativeSlice<MapChunkData>    mapChunkData;

        public void Execute()
        {
            var cellIdValue         = this.cellId.Value;
            var meta                = cellMetas[cellIdValue];

            var cellModuleWeights   = weights.Slice(cellIdValue * this.numModules, this.numModules);
            meta.moduleId           = this.pickRandomModule(cellModuleWeights, this.rng * cellModuleWeights.Sum());
            meta.isCollapsed        = true;
            meta.entropy            = 0.0f;

            // update cell meta data in array
            cellMetas[cellIdValue]  = meta;

            var tile = this.mapChunkData[this.cellId.Value];
            tile.moduleId = meta.moduleId;
            this.mapChunkData[this.cellId.Value] = tile;
        }

        private int pickRandomModule([ReadOnly]NativeSlice<float> distribution, float rng)
        {
            float cumsum = 0.0f;
            for(int moduleId = 0; moduleId < distribution.Length; moduleId++)
            {
                // zero values must be ignored
                if(distribution[moduleId] <= Step3.EPSILONE) { continue; }

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

        public NativeReference<bool>            hasFailed;

        public int                              numModules;

        public NativeArray<CellMeta>            cellMetas;

        public int                              width;
        public int                              height;

        public NativeArray<float>               weights;
        public NativeArray<int>                 stack;

        private int                             stackPtr;
        private int                             moduleChunkSize;


        /// <summary>
        /// Update cellId's cell possible modules that can be on the "side" of "modules".
        /// </summary>
        /// <param name="cellId"></param>
        /// <param name="modules"></param>
        /// <param name="side"></param>
        private void propagate(int cellId, in int[] modules, ModuleConstraints.Side side)
        {
            var cell = this.cellMetas[cellId];
            if(!cell.isCollapsed && !cell.propagated)
            {
                cell.propagated = true;
                this.cellMetas[cellId] = cell;


                var weights = this.weights.Slice(cellId * this.numModules, this.numModules);
                var w1 = 0.0f;
                var w2 = 0.0f;

                for(int moduleB = 0; moduleB < this.numModules; moduleB++)
                {
                    if(weights[moduleB] <= Step3.EPSILONE) { continue; }

                    ulong allowed = 0;
                    foreach(int moduleA in modules)
                    {
                        allowed |= this.constraints.isSet(moduleB, side, moduleA);
                    }

                    w1 += weights[moduleB];
                    weights[moduleB] *= allowed;
                    w2 += weights[moduleB];
                }

                if((w1 - w2) > Step3.EPSILONE)
                {
                    this.stack[stackPtr++] = cellId;
                }
            }
        }

        public void Execute()
        {
            this.moduleChunkSize        = this.numModules * this.numModules;
            this.stackPtr               = 0;

            this.stack[stackPtr++]      = this.collapsedCellId.Value;

            while(stackPtr > 0)
            {
                var cellId              = this.stack[--stackPtr];
                var cell                = this.cellMetas[cellId];

                var modules             = cell.moduleId != -1
                    ? new int[] { cell.moduleId }
                    : this.weights.Slice(cellId * this.numModules, this.numModules)
                        .Select((weight, moduleId) => weight > 0.0f ? moduleId : -1)
                        .Where(moduleId => moduleId != -1).ToArray();

                // if modeules empty -> no solution!
                if(modules.Length == 0)
                {
                    this.hasFailed.Value = true;
                    return;
                }

                // LEFT
                var cellIdLeft = cellId - 1;
                if((cellIdLeft % this.width) != (this.width - 1) && cellIdLeft >= 0)
                {
                    this.propagate(cellIdLeft, modules, ModuleConstraints.Side.Left);
                }

                // RIGHT
                var cellIdRight = cellId + 1;
                if((cellIdRight % this.width) != 0)
                {
                    this.propagate(cellIdRight, modules, ModuleConstraints.Side.Right);
                }

                // TOP
                var cellIdTop = cellId - this.width;
                if(cellIdTop >= 0)
                {
                    this.propagate(cellIdTop, modules, ModuleConstraints.Side.Top);
                }

                // BOTTOM
                var cellIdBottom = cellId + this.width;
                if(cellIdBottom < this.cellMetas.Length)
                {
                    this.propagate(cellIdBottom, modules, ModuleConstraints.Side.Bottom);
                }
            }
        }

    }

    [BurstCompile]
    private struct HasUncollapsedJob : IJob
    {
        [ReadOnly]
        public NativeArray<CellMeta>    cellMetas;

        public NativeReference<bool>    hasNotCollapsedCells;

        [BurstCompile]
        public void Execute()
        {
            for(int i = 0; i < (this.cellMetas.Length); i++)
            {
                if(!this.cellMetas[i].isCollapsed) { this.hasNotCollapsedCells.Value = true; break; }
            }
        }
    }

    [BurstCompile]
    private struct HasSolutionJob : IJob
    {
        [ReadOnly]
        public NativeArray<CellMeta>    cellMetas;

        public NativeReference<bool>    hasSolution;

        [BurstCompile]
        public void Execute()
        {
            for(int i = 0; i < (this.cellMetas.Length); i++)
            {
                if(this.cellMetas[i].moduleId == -1)
                {
                    this.hasSolution.Value = false; break;
                }
            }
        }
    }

    public delegate void Step3Progress(in MapGenData data);
    public event Step3Progress OnProgress;


    private ModuleConstraints       constraints;
    private NativeReference<bool>   hasUncollapsedCells;
    private NativeReference<bool>   hasFailed;
    private NativeReference<bool>   hasSolution;
    private NativeArray<CellMeta>   cells;
    private NativeArray<ModuleMeta> modules;
    private NativeArray<float>      weights;

    private NativeQueue<int>        minEntropyQueue;
    private NativeReference<int>    minEntropy;
    private NativeArray<int>        stack;

    private JobHandle               pendingJob;

    private int                     numModules { get { return this.modules.Length; } }

    public override IEnumerator<MapGenStepState> execute(MapGeneratorSettings context)
    {
        this.modules                                = new NativeArray<ModuleMeta>(context.modules.Select(m => (ModuleMeta)m).ToArray(), Allocator.Persistent);

        // initialize module contraints
        this.constraints                            = new ModuleConstraints(this.modules.AsReadOnly());
        var constraintJob                           = new InitializeVisualConstraintsJob
        {
            constraints                             = constraints,
            modules                                 = this.modules
        }.Schedule(this.numModules, this.numModules);

        // reusable flags
        this.hasUncollapsedCells                    = new NativeReference<bool>(Allocator.Persistent);
        this.hasFailed                              = new NativeReference<bool>(Allocator.Persistent);
        this.hasSolution                            = new NativeReference<bool>(Allocator.Persistent);
        this.minEntropyQueue                        = new NativeQueue<int>(Allocator.Persistent);
        this.minEntropy                             = new NativeReference<int>(Allocator.Persistent);

        for(int chunkId = 0; chunkId < context.data.numMapChunks; chunkId++)
        {
            var numFailedAttempts                   = 0;
            var chunkInfo                           = context.data.getChunkInfo(chunkId);
            var chunkData                           = context.data.getChunkData(chunkId);

            // TODO: account for a row/column overlap with previous map chunk

            this.cells                              = new NativeArray<CellMeta>(chunkInfo.dataSize, Allocator.Persistent);
            this.weights                            = new NativeArray<float>(chunkInfo.dataSize * this.numModules, Allocator.Persistent);
            this.stack                              = new NativeArray<int>(chunkInfo.dataSize, Allocator.Persistent);
           
            while(numFailedAttempts < this.settings.numAttemptsToSolveMapChunk)
            {
                // reset cell meta
                cells.CopyFrom(Enumerable.Range(0, chunkInfo.dataSize).Select(_ => CellMeta.Create()).ToArray());

                // set initial weights acording to current map chunk mask
                var initializeWeights               = new InitializeWeightsJob
                {
                    weights                         = weights,
                    mapChunkData                    = chunkData,
                    modules                         = this.modules,
                }.ScheduleBatch(chunkInfo.dataSize, chunkInfo.bounds.width, constraintJob);

                // if all weights zero -> no more work
                hasUncollapsedCells.Value           = true;
                hasFailed.Value                     = false;
                while(!hasFailed.Value && hasUncollapsedCells.Value)
                {
                    var calculateEntropiesJob       = new ComputeEntropiesJob
                    {
                        weights                     = this.weights,
                        cellMetas                   = this.cells,
                        numModules                  = this.numModules
                    }.ScheduleBatch(chunkInfo.dataSize, chunkInfo.bounds.width, initializeWeights);

                    // find min entropy cell id
                    JobHandle findMinEntropyCellId;
                    {
                        findMinEntropyCellId        = new FindMinEntropyCellPartialJob
                        {
                            cellMetas               = this.cells,
                            minEntropy              = this.minEntropyQueue.AsParallelWriter(),
                        }.ScheduleBatch(this.cells.Length, 1024, calculateEntropiesJob);

                        findMinEntropyCellId        = new FindMinEntropyCellJob
                        {
                            cellMetas               = this.cells,
                            minEntropies            = this.minEntropyQueue,
                            cellId                  = this.minEntropy
                        }.Schedule(findMinEntropyCellId);
                    }

                    var collapseCellJob             = new CollapseCellJob
                    {
                        weights                     = weights,
                        cellId                      = this.minEntropy,
                        cellMetas                   = cells,
                        numModules                  = numModules,
                        rng                         = (float)context.random.NextDouble(),

                        mapChunkData                = chunkData
                    }.Schedule(findMinEntropyCellId);

                    var propagateJob                = new PropagateContraintsJob
                    {
                        numModules                  = this.numModules,
                        width                       = chunkInfo.bounds.width,
                        height                      = chunkInfo.bounds.height,
                        collapsedCellId             = this.minEntropy,
                        cellMetas                   = this.cells,
                        weights                     = this.weights,
                        constraints                 = this.constraints,
                        stack                       = this.stack,
                        hasFailed                   = this.hasFailed
                    }.Schedule(collapseCellJob);

                    // check if all cells are collapsed now
                    this.hasUncollapsedCells.Value  = false;
                    var checkAllCollapsedJob        = new HasUncollapsedJob
                    {
                        cellMetas                   = cells,
                        hasNotCollapsedCells        = hasUncollapsedCells
                    }.Schedule(propagateJob);

                    this.pendingJob = checkAllCollapsedJob;
                    while(!checkAllCollapsedJob.IsCompleted) { yield return new MapGenStepState {}; }
                    checkAllCollapsedJob.Complete();

                    // report progress
                    this.OnProgress?.Invoke(context.data);
                }

                // check, if all cells have been successfully collapsed
                hasSolution.Value   = true;
                var hasSolutionJob  = new HasSolutionJob
                {
                    cellMetas       = cells,
                    hasSolution     = hasSolution
                }.Schedule();

                this.pendingJob = hasSolutionJob;
                while(!hasSolutionJob.IsCompleted) { yield return new MapGenStepState {}; }
                hasSolutionJob.Complete();

                // if we have a solution we are done and can exit loop, else we increase the failed attempt count and try again
                if(hasSolution.Value)
                {
                    break;
                }
                else
                {
                    numFailedAttempts++;
                }
            }

            if(hasSolution.Value)
            {
                Debug.Log($"Map generation successfull. [Attepts: {numFailedAttempts + 1}]");
            }
            else
            {
                Debug.LogWarning("Map generation failed.");
            }

            this.weights.Dispose();
            this.cells.Dispose();
            this.stack.Dispose();
        }

        //string buf = "";
        //for(int i = 0; i < this.modules.Length; i++)
        //{
        //    buf += $"ModuleId: {i} = {this.modules[i].name.ToString()}\n";
        //}

        //buf += "M |";
        //for(int moduleA = 0; moduleA < this.modules.Length; moduleA++)
        //{
        //    for(int moduleB = 0; moduleB < this.modules.Length; moduleB++)
        //    {
        //        buf += $" {moduleB}";
        //    }
        //    buf += " |";
        //}

        //buf += "\n";
        //for(int side = 0; side < 4; side++)
        //{
        //    buf += $"{((ModuleConstraints.Side)side).ToString()[0]} |";
        //    for(int moduleA = 0; moduleA < this.modules.Length; moduleA++)
        //    {
        //        for(int moduleB = 0; moduleB < this.modules.Length; moduleB++)
        //        {
        //            buf += $" {this.constraints.isSet(moduleA, (ModuleConstraints.Side)side, moduleB)}";
        //        }

        //        buf += " |";
        //    }
        //    buf += "\n";
        //}
        //Debug.Log(buf);

        this.hasUncollapsedCells.Dispose();
        this.hasFailed.Dispose();
        this.hasSolution.Dispose();
        this.constraints.Dispose();
        this.minEntropyQueue.Dispose();
        this.minEntropy.Dispose();

        foreach(var module in this.modules) { module.Dispose(); }
        this.modules.Dispose();
    }

    public override void initialize(MapGeneratorSettings context)
    {
    }

    public override void release(MapGeneratorSettings context)
    {
        if(!this.pendingJob.IsCompleted)
        {
            this.pendingJob.Complete();
        }
    }

    public override void Dispose()
    {
        if(this.modules.IsCreated)
        {
            foreach(var module in this.modules) { module.Dispose(); }
            this.modules.Dispose();
        }

        if(this.weights.IsCreated) this.weights.Dispose();
        if(this.cells.IsCreated) this.cells.Dispose();
        if(this.hasUncollapsedCells.IsCreated) this.hasUncollapsedCells.Dispose();
        if(this.hasFailed.IsCreated) this.hasFailed.Dispose();
        if(this.hasSolution.IsCreated) this.hasSolution.Dispose();
        if(this.constraints.IsCreated) this.constraints.Dispose();
        if(this.minEntropyQueue.IsCreated) this.minEntropyQueue.Dispose();
        if(this.minEntropy.IsCreated) this.minEntropy.Dispose();
        if(this.stack.IsCreated) this.stack.Dispose();
    }
}
