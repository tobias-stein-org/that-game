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

public partial struct MapGenData
{
}


public class ColorComparer
{
    public static float deltaE(in Color c1, in Color c2)
    {
        //return 1.0f - (Vector3.Distance(new Vector3(c1.r, c1.g, c1.b), new Vector3(c2.r, c2.g, c2.b)) / Mathf.Sqrt(3f));

        return DE00(RGBToLab(c1), RGBToLab(c2));
        //return DE94(RGBToLab(c1), RGBToLab(c2));
        //return DE76(RGBToLab(c1), RGBToLab(c2));
    }

    private static float Fxyz(float t)
    {
        return t > 0.008856f ? Mathf.Pow(t, 1.0f / 3.0f) : (903.3f * t + 16.0f) / 116.0f;
    }

    // Helper method to convert RGB to Lab color space using SIMD
    public static float3 RGBToLab(in Color color)
    {
        float3 c        = new float3(color.r, color.g, color.b);
        float3 xyz      = new float3(
            math.dot(new float3(0.4124564f, 0.3575761f, 0.1804375f), c),
            math.dot(new float3(0.2126729f, 0.7151522f, 0.0721750f), c),
            math.dot(new float3(0.0193339f, 0.1191920f, 0.9503041f), c)
        ) / 255f;

        float3 fxyz     = math.select(math.pow(xyz / 0.950456f, 1.0f / 3.0f), (xyz * 7.787f) + (16f / 116f), xyz > 0.008856f);
        float3 lab      = new float3((116f * fxyz.y) - 16f, 500f * (fxyz.x - fxyz.y), 200f * (fxyz.y - fxyz.z));

        return lab;
    }

    private static float deltaPrime(float C1Prime, float C2Prime, float h1Prime, float h2Prime)
    {
        float deltaHPrime = h2Prime - h1Prime;

        if (C1Prime * C2Prime == 0)
        {
            // If either C1' or C2' is zero, set deltaH' to 0
            return 0.0f;
        }
        else if (Mathf.Abs(deltaHPrime) <= 180.0f)
        {
            // If the absolute value of deltaH' is less than or equal to 180 degrees, return deltaH'
            return deltaHPrime;
        }
        else if (deltaHPrime > 180.0f)
        {
            // If deltaH' is greater than 180 degrees, subtract 360 degrees
            return deltaHPrime - 360.0f;
        }
        else
        {
            // If deltaH' is less than -180 degrees, add 360 degrees
            return deltaHPrime + 360.0f;
        }
    }

    private static float hPrime(float b, float aPrime)
    {
        float hPrime = Mathf.Atan2(b, aPrime) * 180.0f / Mathf.PI;

        if (hPrime < 0)
        {
            // If h' is negative, add 360 degrees to bring it within the range of 0 to 360 degrees
            hPrime += 360.0f;
        }

        return hPrime;
    }


    public static float DE76(float3 lab1, float3 lab2)
    {
         return Mathf.Sqrt(Mathf.Pow(lab2.x - lab1.x, 2) + Mathf.Pow(lab2.y - lab1.y, 2) + Mathf.Pow(lab2.z - lab1.z, 2));
    }


    public static float DE94(float3 lab1, float3 lab2)
    {

        // Calculate the color difference using the CIE94 formula
        float deltaL = lab2.x - lab1.x;
        float deltaA = lab2.y - lab1.y;
        float deltaB = lab2.z - lab1.z;

        float deltaC = Mathf.Sqrt(deltaA * deltaA + deltaB * deltaB);
        float deltaH = Mathf.Sqrt(deltaA * deltaA + deltaB * deltaB - deltaC * deltaC);

        float Sl = 1.0f;
        float K1 = 0.045f;
        float K2 = 0.015f;

        float Sc = 1.0f + K1 * deltaC;
        float Sh = 1.0f + K2 * deltaC;

        float deltaE = Mathf.Sqrt(
            Mathf.Pow(deltaL / Sl, 2) +
            Mathf.Pow(deltaC / Sc, 2) +
            Mathf.Pow(deltaH / Sh, 2)
        );

        return float.IsNaN(deltaE) ? 0.0f : deltaE;
    }

    const float DEG2RAD = Mathf.PI / 180.0f;

    public static float DE00(float3 ca, float3 cb, float kL = 1.0f, float kC = 1.0f, float kH = 1.0f)
    {
        float L1 = ca.x; float L2 = cb.x;
        float a1 = ca.y; float a2 = cb.y;
        float b1 = ca.z; float b2 = cb.z;


        float C1 = Mathf.Sqrt(a1 * a1 + b1 * b1);
        float C2 = Mathf.Sqrt(a2 * a2 + b2 * b2);
        float barC = (C1 + C2) * 0.5f;

        float G = 0.5f * (1.0f - Mathf.Sqrt(Mathf.Pow(barC, 7.0f) / (Mathf.Pow(barC, 7.0f) + Mathf.Pow(25.0f, 7.0f))));
        float a1Prime = (1.0f + G) * a1;
        float a2Prime = (1.0f + G) * a2;

        float C1Prime = Mathf.Sqrt(a1Prime * a1Prime + b1 * b1);
        float C2Prime = Mathf.Sqrt(a2Prime * a2Prime + b2 * b2);

        float h1Prime = hPrime(b1, a1Prime);
        float h2Prime = hPrime(b2, a2Prime);

        float deltaLPrime = L2 - L1;
        float deltaCPrime = C2Prime - C1Prime;
        float deltahPrime = deltaPrime(C1Prime, C2Prime, h1Prime, h2Prime);
        float deltaHPrimeBar = 2.0f * Mathf.Sqrt(C1Prime * C2Prime) * Mathf.Sin(deltahPrime * 0.5f * DEG2RAD);

        float LPrimeBar = (L1 + L2) * 0.5f;
        float CPrimeBar = (C1Prime + C2Prime) * 0.5f;

        float hPrimeBar = Mathf.Abs(h1Prime - h2Prime) <= 180.0f ? (h1Prime + h2Prime) * 0.5f : (h1Prime + h2Prime + 360.0f) * 0.5f;

        float T = 1.0f -
            0.17f * Mathf.Cos((hPrimeBar - 30.0f)           * DEG2RAD) +
            0.24f * Mathf.Cos(2.0f * hPrimeBar              * DEG2RAD) +
            0.32f * Mathf.Cos((3.0f * hPrimeBar + 6.0f)     * DEG2RAD) -
            0.20f * Mathf.Cos((4.0f * hPrimeBar - 63.0f)    * DEG2RAD);

        float SL = 1.0f + (0.015f * ((LPrimeBar - 50.0f) * (LPrimeBar - 50.0f))) / Mathf.Sqrt(20.0f + ((LPrimeBar - 50.0f) * (LPrimeBar - 50.0f)));
        float SC = 1.0f + 0.045f * CPrimeBar;
        float SH = 1.0f + 0.015f * CPrimeBar * T;

        float deltaTheta = 30.0f * Mathf.Exp(-Mathf.Pow((hPrimeBar - 275.0f) / 25.0f, 2.0f));
        float RC = 2.0f * Mathf.Sqrt(Mathf.Pow(CPrimeBar, 7.0f) / (Mathf.Pow(CPrimeBar, 7.0f) + Mathf.Pow(25.0f, 7.0f)));
        float RT = -RC * Mathf.Sin(2.0f * deltaTheta * DEG2RAD);

        float deltaE =
            Mathf.Sqrt(
                Mathf.Pow(deltaLPrime       / (kL * SL), 2.0f) +
                Mathf.Pow(deltaCPrime       / (kC * SC), 2.0f) +
                Mathf.Pow(deltaHPrimeBar    / (kH * SH), 2.0f) +
                RT * (deltaCPrime           / (kC * SC))
                   * (deltaHPrimeBar        / (kH * SH))
            );

        return deltaE;
    }
}


public class Step3 : MapGenStep<Step3Settings>
{
    public  const float                     EPSILONE = 1e-6f;

    internal struct Step3State
    {
        private UnsafeBitArray              state;

        public  bool IsCreated { get { return this.state.IsCreated; } }
        public  void Dispose()
        {
            if(this.state.IsCreated) { this.state.Dispose(); }
        }

        public static Step3State    Create()
        {
            return new Step3State
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
                foreach(var pixel in module.sprite.texture.GetPixels(x, y, width, height)) { pixels.AddNoResize(pixel); }

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
    public  struct ModuleConstraints : IDisposable
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
    public  struct Grid : IDisposable
    {
        [NativeDisableContainerSafetyRestriction]
        private UnsafeHashMap<MapGenData.Layer, UnsafeList<GridCell>>    layers;

        public  bool                    IsCreated   { get { return this.layers.IsCreated; } }

        public  int                     width       { get; private set; }
        public  int                     height      { get; private set; }
        public  int                     size        { get { return this.width * this.height; } }

        public static Grid Create(int width, int height, IEnumerable<MapGenData.Layer> layers = null)
        {
            var instance = new Grid
            {
                width   = width,
                height  = height,
                layers  = new UnsafeHashMap<MapGenData.Layer, UnsafeList<GridCell>>(MapGenData.Layer.MAX_LAYERS, Allocator.Persistent)
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

        public void addLayer(MapGenData.Layer layer)
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

        public void reset(MapGenData.Layer layer)
        {
            var cells = this.layers[layer];
            for(int cellId = 0; cellId < this.size; cellId++) { cells.ElementAt(cellId).reset(); } 
        }

        #region Grid cell access

        public ref GridCell this[MapGenData.Layer layer, int index] { get { return ref this.layers[layer].ElementAt(index); } }

        #endregion // Grid cell access
    }

    [BurstCompile]
    public  unsafe struct GridCell
    {
        public  bool                isEmpty { get; private set; }
        public  bool                isCollapsed;
        private bool                canReset;

        public  int                 moduleId;
        public  float               entropy;

        private MapGenData.Layer    layer;
        private MapChunkData*       mapDataPtr;

        public static GridCell Create(MapChunkData* mapDataPtr, MapGenData.Layer layer)
        {
            // get the current moduleId set on this cell, this will most of the time be -1, but in case we are dealing with a neighbouring tile
            // of the current processed chunk, it could already been processed and set to a non negative value
            var moduleId = (*mapDataPtr)[layer];
           
            return new GridCell
            {
                isEmpty                 = false,

                // note: we make an assumption here, that if the tile module of the current referenced tile is unset or set
                // it will also be the case for all other layers
                canReset                = moduleId == -1,
                isCollapsed             = moduleId != -1 ? true : false,
                moduleId                = moduleId,
                entropy                 = 0.0f,

                layer                   = layer,

                mapDataPtr              = mapDataPtr,
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
                    moduleId            = -1,
                    entropy             = 0.0f,

                    mapDataPtr          = null,
                };
            }
        }

        public void reset()
        {
            if(this.canReset)
            {
                this.isCollapsed        = false;
                this.entropy            = 0.0f;
                this.moduleId           = -1;
            }
        }

        public void apply()
        {
            if(this.isEmpty)
            {
                throw new Exception($"Cannot apply GridCell changes to an empty cell.");
            }

            var mapData                 = this.mapData;
            mapData[this.layer]         = this.moduleId;
            this.mapData                = mapData;
        }

        public MapChunkData mapData
        {
            get         { return *(this.mapDataPtr); }
            private set { *(this.mapDataPtr) = value; }
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

        private void adjustModuleWeight(int moduleA)
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

        private void check(int moduleA, ModuleConstraints.Side side, int moduleB)
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

                case ModuleConstraints.Side.Top:
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

                case ModuleConstraints.Side.Bottom:
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


                similar[i] = Mathf.Clamp(ColorComparer.deltaE(borderA[i], borderB[i]), 0.0f, this.similarityThreshold);
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
        public  Grid                        grid;
        public  MapGenData.Layer            layer;

        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkData;

        [ReadOnly]
        public  MapChunkInfo                chunkInfo;

        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkLeft;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkRight;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkTop;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkBottom;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkTopLeft;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkTopRight;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkBottomLeft;
        [ReadOnly]
        public  NativeSlice<MapChunkData>   chunkBottomRight;

        private bool hasLeft        { get { return this.chunkLeft.Length        > 0; } }
        private bool hasRight       { get { return this.chunkRight.Length       > 0; } }
        private bool hasTop         { get { return this.chunkTop.Length         > 0; } }
        private bool hasBottom      { get { return this.chunkBottom.Length      > 0; } }

        private bool hasTopLeft     { get { return this.chunkTopLeft.Length     > 0; } }
        private bool hasTopRight    { get { return this.chunkTopRight.Length    > 0; } }
        private bool hasBottomLeft  { get { return this.chunkBottomLeft.Length  > 0; } }
        private bool hasBottomRight { get { return this.chunkBottomRight.Length > 0; } }

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
                        this.grid[this.layer, cellId] = this.hasTopLeft ? GridCell.Create((MapChunkData*)this.chunkTopLeft.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                        continue;
                    }
                    // top-right
                    if(x == this.grid.width - 1 && y == 0                    && this.hasRight && this.hasTop)
                    {
                        this.grid[this.layer, cellId] = this.hasTopRight ? GridCell.Create((MapChunkData*)this.chunkTopRight.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                        continue;
                    }
                    // bottom-left
                    if(x == 0                   && y == this.grid.height - 1 && this.hasLeft  && this.hasBottom)
                    {
                        this.grid[this.layer, cellId] = this.hasBottomLeft ? GridCell.Create((MapChunkData*)this.chunkBottomLeft.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                        continue;
                    }
                    // bottom-right
                    if(x == this.grid.width - 1 && y == this.grid.height - 1 && this.hasRight && this.hasBottom)
                    {
                        this.grid[this.layer, cellId] = this.hasBottomRight ? GridCell.Create((MapChunkData*)this.chunkBottomRight.GetUnsafeReadOnlyPtr(), this.layer) : GridCell.Empty;
                        continue;
                    }

                    // chunk adjusted tile (x,y) coord
                    var cx                              = this.hasLeft ? x - 1 : x;
                    var cy                              = this.hasTop  ? y - 1 : y;

                    // left
                    if(this.hasLeft             && x == 0)
                    {
                        this.grid[this.layer, cellId]   = GridCell.Create((MapChunkData*)this.chunkLeft.GetUnsafeReadOnlyPtr() + ((cy * this.chunkInfo.bounds.width) + this.chunkInfo.bounds.width - 1), this.layer);
                        continue;
                    }

                    // right
                    if(this.hasRight            && x == this.grid.width - 1)
                    {
                        this.grid[this.layer, cellId]   = GridCell.Create((MapChunkData*)this.chunkRight.GetUnsafeReadOnlyPtr() + (cy * this.chunkInfo.bounds.width), this.layer);
                        continue;
                    }

                    // top
                    if(this.hasTop              && y == 0)
                    {
                        this.grid[this.layer, cellId]   = GridCell.Create((MapChunkData*)this.chunkTop.GetUnsafeReadOnlyPtr() + cx, this.layer);
                        continue;
                    }

                    // bottom
                    if(this.hasBottom           && y == this.grid.height - 1)
                    {
                        this.grid[this.layer, cellId]   = GridCell.Create((MapChunkData*)this.chunkBottom.GetUnsafeReadOnlyPtr() + cx, this.layer);
                        continue;
                    }

                    // else process current chunk data
                    var chunkTileId                     = (cy * this.chunkInfo.bounds.width) + cx;
                    this.grid[this.layer, cellId]       = GridCell.Create((MapChunkData*)this.chunkData.GetUnsafeReadOnlyPtr() + chunkTileId, this.layer);
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
        public NativeArray<float>               weights;

        [ReadOnly]
        public Grid                             grid;
        public MapGenData.Layer                 layer;

        public void Execute(int start, int count)
        {
            for(int cellId = start; cellId < start + count; cellId++)
            {
                var cell = this.grid[this.layer, cellId];

                if(cell.isEmpty || cell.isCollapsed) { continue; }
               
                var cellModuleWeight0 = (cellId * this.modules.Length);
                for(int moduleId = 0; moduleId < this.modules.Length; moduleId++)
                {
                    // ONLY, allow 'walkable' modules
                    this.weights[cellModuleWeight0 + moduleId] = this.modules[moduleId].constructionType == TileConstructionType.Walkable
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
        public NativeArray<float>               weights;

        [ReadOnly]
        public Grid                             grid;
        public MapGenData.Layer                 layer;

        public float                            mapChunkObstructivness;

        public void Execute(int start, int count)
        {
            for(int cellId = start; cellId < start + count; cellId++)
            {
                var cell = this.grid[this.layer, cellId];

                if(cell.isEmpty || cell.isCollapsed) { continue; }


                var cellModuleWeight0   = (cellId * this.modules.Length);
                var tileData            = cell.mapData;

                for(int moduleId = 0; moduleId < this.modules.Length; moduleId++)
                {
                    var moduleConstructionType = this.modules[moduleId].constructionType;

                    switch(tileData.constructionType)
                    {
                        // force only obstructable modules to be placed here
                        case TileConstructionType.Obstructed:
                        {
                            this.weights[cellModuleWeight0 + moduleId] = moduleConstructionType == TileConstructionType.Obstructed
                                ? this.modules[moduleId].weight
                                : 0.0f;
                            break;
                        }

                        // never allow obstructables on walkable tiles
                        case TileConstructionType.Walkable:
                        {
                            this.weights[cellModuleWeight0 + moduleId] = moduleConstructionType == TileConstructionType.Obstructed
                                ? 0.0f
                                : this.modules[moduleId].weight;
                            break;
                        }

                        // else everything goes
                        default:
                        {
                            this.weights[cellModuleWeight0 + moduleId] = moduleConstructionType == TileConstructionType.Obstructed
                                    ? this.modules[moduleId].weight * this.mapChunkObstructivness
                                    //? 0.0f
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
        public MapGenData.Layer                 layer;

        public void Execute(int startIndex, int count)
        {
            for(int i = startIndex; i < startIndex + count; i++)
            { 
                if(!this.grid[this.layer, i].isEmpty && this.grid[this.layer, i].moduleId != -1 && this.modules[this.grid[this.layer, i].moduleId].constructionType != TileConstructionType.Obstructed)
                {
                    this.grid[this.layer, i].moduleId = -1;
                    this.grid[this.layer, i].apply();
                }
            }
        }
    }

    [BurstCompile]
    private struct ComputeEntropiesJob : IJobParallelForBatch
    {
        public Grid                     grid;
        public MapGenData.Layer         layer;

        [ReadOnly]
        public NativeArray<float>       weights;

        public int                      numModules;

        public void Execute(int startIndex, int count)
        {
            for(int i = 0; i < count; i++)
            {
                var cellId  = startIndex + i;

                // empty or collapsed cells are ignored
                if(this.grid[this.layer, cellId].isEmpty || this.grid[this.layer, cellId].isCollapsed) { continue; }

                var weights                             = this.weights.Slice(cellId * numModules, numModules);

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
                this.grid[this.layer, cellId].entropy   = fastLog2(sum) - (logSum / sum + Step3.EPSILONE);
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
        public Grid                             grid;
        public MapGenData.Layer                 layer;

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
        public MapGenData.Layer     layer;

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
        public MapGenData.Layer                 layer;

        [ReadOnly]
        public NativeArray<float>               weights;

        public float                            rng;
        public int                              numModules;

        [ReadOnly]
        public NativeReference<int>             cellId;

        public void Execute()
        {
            var cellId                     = this.cellId.Value;
            if(cellId < 0) { return; }

            var cellModuleWeights                       = weights.Slice(cellId * this.numModules, this.numModules);
            this.grid[this.layer, cellId].moduleId      = this.pickRandomModule(cellModuleWeights, this.rng * cellModuleWeights.Sum());
            this.grid[this.layer, cellId].isCollapsed   = true;
            this.grid[this.layer, cellId].entropy       = 0.0f;
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

        public NativeReference<Step3State>      state;

        public int                              numModules;

        public Grid                             grid;
        public MapGenData.Layer                 layer;

        public NativeArray<float>               weights;
        public NativeArray<int>                 stack;

        private int                             stackPtr;

        /// <summary>
        /// Update cellId's cell possible modules that can be on the "side" of "modules".
        /// </summary>
        /// <param name="cellId"></param>
        /// <param name="modules"></param>
        /// <param name="side"></param>
        private void propagate(int cellId, in int[] modules, ModuleConstraints.Side side)
        {
            var cell = this.grid[this.layer, cellId];
            if(!cell.isCollapsed)
            {
                //Debug.Log($"Propagating to {side.ToString()} cell [ID: {cellId}]: {String.Join(",", modules)}");

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

                //Debug.Log($"Remaining options {side.ToString()} cell {cellId}: {String.Join(",", weights.Select((w, moduleId) => w > Step3.EPSILONE ? moduleId : -1).Where(id => id != -1))}");

                if((w1 - w2) > Step3.EPSILONE)
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
                var modules             = cell.moduleId != -1
                    ? new int[] { cell.moduleId }
                    : this.weights.Slice(cellId * this.numModules, this.numModules)
                        .Select((weight, moduleId) => weight > 0.0f ? moduleId : -1)
                        .Where(moduleId => moduleId != -1).ToArray();

              
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
        public MapGenData.Layer                 layer;

        public void Execute()
        {
            if(this.cellId.Value != -1) { this.grid[this.layer, this.cellId.Value].apply(); }
        }
    }

    [BurstCompile]
    private struct AllCellsCollapsedJob : IJob
    {
        public Grid                         grid;
        public MapGenData.Layer             layer;

        public NativeReference<Step3State>  state;

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
        public MapGenData.Layer             layer;

        public NativeReference<Step3State>  state;

        [BurstCompile]
        public void Execute()
        {
            this.state.Value.hasSolution(true);
            for(int i = 0; i < (this.grid.size); i++)
            {
                if(!this.grid[this.layer, i].isEmpty && this.grid[this.layer, i].moduleId == -1)
                {
                    this.state.Value.hasSolution(false);
                    break;
                }
            }
        }
    }

    public  delegate void                           Step3Progress(in MapGenData data);
    public  event Step3Progress                     OnProgress;
                                                    
                                                    
    private ModuleConstraints                       constraints;
    private ModuleConstraints                       passTwoConstraints;

    private NativeReference<Step3State>             state;
                                                    
    private Grid                                    grid;

    private NativeArray<ModuleMeta>                 modules;
    private NativeArray<float>                      weights;
                                                    
    private NativeQueue<int>                        minEntropyQueue;
    private NativeReference<int>                    minEntropyCell;
    private NativeArray<int>                        stack;


    public override IEnumerator<MapGenStepState> execute(MapGeneratorSettings context)
    {
        // initialize module contraints
        this.constraints                            = new ModuleConstraints(this.modules.AsReadOnly());
        this.passTwoConstraints                     = new ModuleConstraints(this.modules.AsReadOnly());

        var constraintsJob                          = new InitializeConstraintsJob
        {
            similarityThreshold                     = this.settings.similarityThreshold,
            similarityPercentile                    = this.settings.similarityPercentile,
            constraints                             = this.constraints,
            modules                                 = this.modules
        };

        var dependsOn = context.schedule(constraintsJob, this.modules.Length, this.modules.Length);

        for(int chunkId = 0; chunkId < context.data.numMapChunks; chunkId++)
        {
            this.state.Value.reset();

            var initGrid = this.initializeGrid(chunkId, context);
            while(initGrid.MoveNext()) { yield return initGrid.Current; }

            this.weights                            = new NativeArray<float>(this.grid.size * this.modules.Length, Allocator.Persistent);
            this.stack                              = new NativeArray<int>(this.grid.size * this.grid.size, Allocator.Persistent);

            // 1st pass - generate floor
            var passOne = this.passOne(context, this.grid, dependsOn);
            while(passOne.MoveNext()) { yield return passOne.Current; }

            // 2nd pass - generate obstructables
            var passTwo = this.passTwo(context, this.grid, dependsOn);
            while(passTwo.MoveNext()) { yield return passTwo.Current; }

            if(this.state.Value.hasSolution())
                Debug.Log($"Map generation successfull. [Attepts: {this.state.Value.numFails() + 1}]");
            else
                Debug.LogWarning("Map generation failed.");

            this.weights.Dispose();
            this.grid.Dispose();
            this.stack.Dispose();
        }
    }

    private IEnumerator<MapGenStepState> initializeGrid(int chunkId, MapGeneratorSettings context)
    {
        var chunkInfo                               = context.data.getChunkInfo(chunkId);
        var chunkData                               = context.data.getChunkData(chunkId);
        
        var chunkInfoLeft                           = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(-chunkInfo.bounds.width, 0));
        var chunkInfoRight                          = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(chunkInfo.bounds.width, 0));
        var chunkInfoTop                            = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(0, -chunkInfo.bounds.height));
        var chunkInfoBottom                         = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(0, chunkInfo.bounds.height));

        var chunkInfoTopLeft                        = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(-chunkInfo.bounds.width, -chunkInfo.bounds.height));
        var chunkInfoTopRight                       = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(chunkInfo.bounds.width, -chunkInfo.bounds.height));
        var chunkInfoBottomLeft                     = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(-chunkInfo.bounds.width, chunkInfo.bounds.height));
        var chunkInfoBottomRight                    = context.data.getChunkInfo(chunkInfo.bounds.position + new Vector2Int(chunkInfo.bounds.width, chunkInfo.bounds.height));

        // note: this will ensure we only use already generated neighbouring cells
        if(chunkInfoLeft.HasValue && chunkInfoLeft.Value.id > chunkInfo.id) { chunkInfoLeft = null; }
        if(chunkInfoRight.HasValue && chunkInfoRight.Value.id > chunkInfo.id) { chunkInfoRight = null; }
        if(chunkInfoTop.HasValue && chunkInfoTop.Value.id > chunkInfo.id) { chunkInfoTop = null; }
        if(chunkInfoBottom.HasValue && chunkInfoBottom.Value.id > chunkInfo.id) { chunkInfoBottom = null; }
        if(chunkInfoTopLeft.HasValue && chunkInfoTopLeft.Value.id > chunkInfo.id) { chunkInfoTopLeft = null; }
        if(chunkInfoTopRight.HasValue && chunkInfoTopRight.Value.id > chunkInfo.id) { chunkInfoTopRight = null; }
        if(chunkInfoBottomLeft.HasValue && chunkInfoBottomLeft.Value.id > chunkInfo.id) { chunkInfoBottomLeft = null; }
        if(chunkInfoBottomRight.HasValue && chunkInfoBottomRight.Value.id > chunkInfo.id) { chunkInfoBottomRight = null; }

        var gridWidth                               = chunkInfo.bounds.width;
        var gridHeight                              = chunkInfo.bounds.height;

        if(chunkInfoLeft.HasValue)                  { gridWidth++; }
        if(chunkInfoRight.HasValue)                 { gridWidth++; }
        if(chunkInfoTop.HasValue)                   { gridHeight++; }
        if(chunkInfoBottom.HasValue)                { gridHeight++; }


        this.grid                                   = Grid.Create(gridWidth, gridHeight);

        foreach(var layer in new[] { MapGenData.Layer.Floor, MapGenData.Layer.Obstructable })
        {
            this.grid.addLayer(layer);

            var initializeGridJob                   = new InitializeGridJob
            {
                grid                                = this.grid,
                layer                               = layer,
                chunkData                           = chunkData,
                chunkInfo                           = chunkInfo,

                chunkLeft                           = chunkInfoLeft.HasValue
                    // get slice of the bottom border of the top neighbour map chunk
                    ? context.data.mapChunkData.Slice(chunkInfoLeft.Value.dataIndex0, chunkInfoLeft.Value.dataSize)
                    : chunkData.Slice(0, 0),
                chunkRight                          = chunkInfoRight.HasValue
                    // get slice of the left border of the right neighbour map chunk
                    ? context.data.mapChunkData.Slice(chunkInfoRight.Value.dataIndex0, chunkInfoRight.Value.dataSize)
                    : chunkData.Slice(0, 0),
                chunkTop                            = chunkInfoTop.HasValue
                    // get slice of the bottom border of the top neighbour map chunk
                    ? context.data.mapChunkData.Slice(chunkInfoTop.Value.dataIndex0 + chunkInfoTop.Value.dataSize - chunkInfoTop.Value.bounds.width, chunkInfoTop.Value.bounds.width)
                    : chunkData.Slice(0, 0),
                chunkBottom                         = chunkInfoBottom.HasValue
                    // get slice of the top border of the bottom neighbour map chunk
                    ? context.data.mapChunkData.Slice(chunkInfoBottom.Value.dataIndex0, chunkInfoBottom.Value.bounds.width)
                    : chunkData.Slice(0, 0),

                chunkTopLeft                        = chunkInfoTopLeft.HasValue
                    ? context.data.mapChunkData.Slice(chunkInfoTopLeft.Value.dataIndex0 + chunkInfoTopLeft.Value.dataSize - 1, 1)
                    : chunkData.Slice(0, 0),
                chunkTopRight                       = chunkInfoTopRight.HasValue
                    ? context.data.mapChunkData.Slice(chunkInfoTopRight.Value.dataIndex0 + chunkInfoTopRight.Value.dataSize - chunkInfoTopRight.Value.bounds.width, 1)
                    : chunkData.Slice(0, 0),
                chunkBottomLeft                     = chunkInfoBottomLeft.HasValue
                    ? context.data.mapChunkData.Slice(chunkInfoBottomLeft.Value.dataIndex0 + chunkInfoBottomLeft.Value.bounds.width - 1, 1)
                    : chunkData.Slice(0, 0),
                chunkBottomRight                    = chunkInfoBottomRight.HasValue
                    ? context.data.mapChunkData.Slice(chunkInfoBottomRight.Value.dataIndex0, 1)
                    : chunkData.Slice(0, 0),

            };

            context.scheduleBatch(initializeGridJob, this.grid.size, this.grid.width);
            yield return new MapGenStepState {};
        }
    }

    private IEnumerator<MapGenStepState> passOne(MapGeneratorSettings context, Grid grid, JobHandle dependsOn)
    {
        this.state.Value.hasSolution(false);

        while(!this.state.Value.hasSolution() && (this.settings.numAttemptsToSolveMapChunk == 0 || this.state.Value.numFails() < this.settings.numAttemptsToSolveMapChunk))
        {
            // reset all grid cells
            grid.reset(MapGenData.Layer.Floor);
            
            // set initial weights acording to current map chunk mask
            var initializeWeightsJob            = new InitializePassOneWeightsJob
            {
                grid                            = this.grid,
                layer                           = MapGenData.Layer.Floor,
                weights                         = this.weights,
                modules                         = this.modules,
            };
            
            dependsOn = context.scheduleBatch(initializeWeightsJob, this.grid.size, this.grid.width, dependsOn);
            
            var WFC = this.runWFC(context, grid, MapGenData.Layer.Floor, dependsOn);
            while(WFC.MoveNext()) { yield return WFC.Current; }
        }
    }

    private IEnumerator<MapGenStepState> passTwo(MapGeneratorSettings context, Grid grid, JobHandle dependsOn)
    {
        this.state.Value.hasSolution(false);

        while(!this.state.Value.hasSolution() && (this.settings.numAttemptsToSolveMapChunk == 0 || this.state.Value.numFails() < this.settings.numAttemptsToSolveMapChunk))
        {
            // reset all grid cells
            grid.reset(MapGenData.Layer.Obstructable);

            // set initial weights acording to current map chunk mask
            var initializeWeightsJob            = new InitializePassTwoWeightsJob
            {
                grid                            = grid,
                layer                           = MapGenData.Layer.Obstructable,
                mapChunkObstructivness          = this.settings.mapChunkObstructivness,          
                weights                         = this.weights,
                modules                         = this.modules,
            };

            dependsOn = context.scheduleBatch(initializeWeightsJob, this.grid.size, this.grid.width, dependsOn);

            var WFC = this.runWFC(context, grid, MapGenData.Layer.Obstructable, dependsOn);
            while(WFC.MoveNext()) { yield return WFC.Current; }

            var postJob = new PassTwoPostJob
            {
                grid                            = grid,
                layer                           = MapGenData.Layer.Obstructable,
                modules                         = this.modules
            };

            context.scheduleBatch(postJob, this.grid.size, this.grid.width, dependsOn);

            yield return new MapGenStepState {};

            this.OnProgress?.Invoke(context.data);
        }
    }

    private IEnumerator<MapGenStepState> runWFC(MapGeneratorSettings context, Grid grid, MapGenData.Layer layer, JobHandle dependsOn)
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
                numModules                  = this.modules.Length,
                collapsedCellId             = this.minEntropyCell,
                weights                     = this.weights,
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

            yield return new MapGenStepState {};

            // report progress
            this.OnProgress?.Invoke(context.data);
        }

        while(!this.state.Value.hasFailed() && !this.state.Value.allCollapsed())
        {
            dependsOn = this.doWaveFunctionCollapse(context, layer, dependsOn);

            var updateMapChunkDataJob       = new UpdateMapChunkDataJob
            {
                cellId                      = this.minEntropyCell,
                grid                        = grid,
                layer                       = layer,
            };
            dependsOn = context.schedule(updateMapChunkDataJob, dependsOn);

            dependsOn = this.checkAllCellsCollapsed(context, layer, dependsOn);

            // hand over controll to pipeline executor
            yield return new MapGenStepState {};

            // report progress
            this.OnProgress?.Invoke(context.data);
        }

        this.checkSolution(context, layer, dependsOn);

        yield return new MapGenStepState { };
    }

    private JobHandle doWaveFunctionCollapse(MapGeneratorSettings context, MapGenData.Layer layer, JobHandle dependsOn)
    {
        var calculateEntropiesJob       = new ComputeEntropiesJob
        {
            grid                        = this.grid,
            layer                       = layer,
            weights                     = this.weights,
            numModules                  = this.modules.Length
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
            weights                     = this.weights,
            cellId                      = this.minEntropyCell,
            numModules                  = this.modules.Length,
            rng                         = (float)context.random.NextDouble(),
        };

        dependsOn = context.schedule(collapseCellJob, dependsOn);

        var propagateJob                = new PropagateContraintsJob
        {
            grid                        = this.grid,
            layer                       = layer,
            numModules                  = this.modules.Length,
            collapsedCellId             = this.minEntropyCell,
            weights                     = this.weights,
            constraints                 = this.constraints,
            stack                       = this.stack,
            state                       = this.state
        };

        return context.schedule(propagateJob, dependsOn);
    }

    private JobHandle checkAllCellsCollapsed(MapGeneratorSettings context, MapGenData.Layer layer, JobHandle dependsOn)
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

    private void checkSolution(MapGeneratorSettings context, MapGenData.Layer layer, JobHandle dependsOn)
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

    public  override void initialize(MapGeneratorSettings context)
    {
        this.state                      = new NativeReference<Step3State>(Step3State.Create(), Allocator.Persistent);
        this.modules                    = new NativeArray<ModuleMeta>(context.modules.Select(m => (ModuleMeta)m).ToArray(), Allocator.Persistent);
        this.minEntropyQueue            = new NativeQueue<int>(Allocator.Persistent);
        this.minEntropyCell             = new NativeReference<int>(Allocator.Persistent);
    }

    public  override void release(MapGeneratorSettings context)
    {
        this.Dispose();
    }

    public  override void Dispose()
    {
        if(this.modules.IsCreated)
        {
            foreach(var module in this.modules) { module.Dispose(); }
            this.modules.Dispose();
        }

        if(this.weights.IsCreated) this.weights.Dispose();
        if(this.grid.IsCreated) this.grid.Dispose();
        if(this.constraints.IsCreated) this.constraints.Dispose();
        if(this.passTwoConstraints.IsCreated) this.passTwoConstraints.Dispose();
        if(this.minEntropyQueue.IsCreated) this.minEntropyQueue.Dispose();
        if(this.minEntropyCell.IsCreated) this.minEntropyCell.Dispose();
        if(this.stack.IsCreated) this.stack.Dispose();

        if(this.state.IsCreated) this.state.Dispose();
    }
}
