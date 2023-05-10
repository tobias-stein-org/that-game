using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Unity.Mathematics;

public partial struct MapGenData
{

}

public class Step3 : MapGenStep<Step3Settings>
{
    private int numModules = 3;

    [BurstCompile]
    private struct CellMeta : IComparable<CellMeta>
    {
        private enum CellMetaStates
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

    public enum ModuleConstraintSide { Left = 0, Right, Top, Bottom }

    [BurstCompile]
    private struct InitializeConstraintsJob : IJobParallelFor
    {
        // TODO
        //[ReadOnly]
        //public NativeArray<> modules;
        public int numModules;

        [NativeDisableParallelForRestriction] 
        public NativeBitArray constraints;

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

        /// <summary>
        /// Allow moduleB to be on the "side" of moduleA. This will automatically also allow the other way around, that is,
        /// allow moduleA to be on the opposite-"side" of moduleB.
        /// </summary>
        /// <param name="moduleA"></param>
        /// <param name="moduleB"></param>
        /// <param name="side"></param>
        private void allow(int moduleB, ModuleConstraintSide side, int moduleA, int moduleAChunkStart, int moduleBChunkStart, int rowLength)
        {
            switch(side)
            {
                case ModuleConstraintSide.Left:
                {
                    this.constraints.Set((int)ModuleConstraintSide.Left     * rowLength + moduleAChunkStart + moduleB, true);
                    this.constraints.Set((int)ModuleConstraintSide.Right    * rowLength + moduleBChunkStart + moduleA, true);
                    break;
                }

                case ModuleConstraintSide.Right:
                {
                    this.constraints.Set((int)ModuleConstraintSide.Right    * rowLength + moduleAChunkStart + moduleB, true);
                    this.constraints.Set((int)ModuleConstraintSide.Left     * rowLength + moduleBChunkStart + moduleA, true);
                    break;
                }

                case ModuleConstraintSide.Top:
                {
                    this.constraints.Set((int)ModuleConstraintSide.Top      * rowLength + moduleAChunkStart + moduleB, true);
                    this.constraints.Set((int)ModuleConstraintSide.Bottom   * rowLength + moduleBChunkStart + moduleA, true);
                    break;
                }

                case ModuleConstraintSide.Bottom:
                {
                    this.constraints.Set((int)ModuleConstraintSide.Bottom   * rowLength + moduleAChunkStart + moduleB, true);
                    this.constraints.Set((int)ModuleConstraintSide.Top      * rowLength + moduleBChunkStart + moduleA, true);
                    break;
                }
            }
        }

        [BurstCompile]
        public void Execute(int moduleA)
        {
            var moduleAChunkStart   = moduleA * this.numModules;
            var rowLength           = this.numModules * this.numModules;

            for(int moduleB = moduleA; moduleB < this.numModules; moduleB++)
            {
                var moduleBChunkStart   = moduleB * this.numModules;

                // TODO: actually compute constraints
                //this.allow(moduleB, ModuleConstraintSide.Left, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                //this.allow(moduleB, ModuleConstraintSide.Right, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                //this.allow(moduleB, ModuleConstraintSide.Top, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                //this.allow(moduleB, ModuleConstraintSide.Bottom, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);

                if(moduleA == 0) // undefined
                {
                    this.allow(moduleB, ModuleConstraintSide.Left, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Right, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Top, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Bottom, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                }
                else if(moduleA == 1) // path
                {
                    this.allow(moduleB, ModuleConstraintSide.Left, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Right, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Top, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Bottom, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                }
                else if(moduleA == 2 && !(moduleB == 1))
                {
                    this.allow(moduleB, ModuleConstraintSide.Left, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    this.allow(moduleB, ModuleConstraintSide.Right, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    //this.allow(moduleB, ModuleConstraintSide.Top, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                    //this.allow(moduleB, ModuleConstraintSide.Bottom, moduleA, moduleAChunkStart, moduleBChunkStart, rowLength);
                }
            }
        }
    }
  
    [BurstCompile]
    private struct InitializeWeightsJob : IJobParallelForBatch
    {
        public int                      numModules;
        public int                      maskHeight;

        [NativeDisableParallelForRestriction]
        public NativeArray<float>       weights;

        [ReadOnly]
        public NativeSlice<TileMask>    mask;

        public void Execute(int maskStartIndex, int maskWidth)
        {
            var weightChunkSize   = maskWidth       * this.numModules;
            var weightChunkStart  = maskStartIndex  * this.numModules;

            for(int i = weightChunkStart; i < weightChunkStart + weightChunkSize; i += this.numModules)
            {
                for(int moduleId = 0; moduleId < this.numModules; moduleId++)
                {
                    this.weights[i + moduleId] = 1.0f;
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

        public const float              EPSILONE = 1e-6f;

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
                cell.entropy            = fastLog2(sum) - (logSum / sum + EPSILONE);
                cell.propagated         = false;
                this.cellMetas[cellId]  = cell;
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
        public NativeSlice<TileMask>            tiles;

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

            this.tiles[this.cellId.Value] = (TileMask)Mathf.Clamp(meta.moduleId, 0, this.numModules - 1);
        }

        private int pickRandomModule([ReadOnly]NativeSlice<float> distribution, float rng)
        {
            float cumsum = 0.0f;
            for(int moduleId = 0; moduleId < distribution.Length; moduleId++)
            {
                // zero values must be ignored
                if(distribution[moduleId] == 0.0) { continue; }

                cumsum += distribution[moduleId];
                if(cumsum >= rng) { return moduleId; }
            }

            return -1;
        }
    }

    private struct PropagateContraintsJob : IJob
    {
        [ReadOnly]
        public NativeBitArray.ReadOnly          constraints;

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


        private void propagate(int cellId, in int[] modules, ModuleConstraintSide side)
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
                    ulong allowed = 0;
                    foreach(int moduleA in modules) { allowed |= this.constraints.GetBits(((int)side * this.moduleChunkSize) + (moduleA * this.numModules) + moduleB); }

                    w1 += weights[moduleB];
                    weights[moduleB] *= allowed;
                    w2 += weights[moduleB];
                }

                if((w1 - w2) > 1e-5f)
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
                    this.propagate(cellIdLeft, modules, ModuleConstraintSide.Left);
                }

                // RIGHT
                var cellIdRight = cellId + 1;
                if((cellIdRight % this.width) != 0)
                {
                    this.propagate(cellIdRight, modules, ModuleConstraintSide.Right);
                }

                // TOP
                var cellIdTop = cellId - this.width;
                if(cellIdTop >= 0)
                {
                    this.propagate(cellIdTop, modules, ModuleConstraintSide.Top);
                }

                // BOTTOM
                var cellIdBottom = cellId + this.width;
                if(cellIdBottom < this.cellMetas.Length)
                {
                    this.propagate(cellIdBottom, modules, ModuleConstraintSide.Bottom);
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

    [BurstCompile]
    private struct PaintTilesJob : IJobParallelForBatch
    {
        [NativeDisableParallelForRestriction]
        public NativeSlice<TileMask> tiles;

        [ReadOnly]
        public NativeArray<CellMeta> cellMetas;

        public int tilesHeight;

        public void Execute(int tilesStartIndex, int tilesWidth)
        {
            for(int i = 0; i < tilesWidth; i++)
            {
                tiles[tilesStartIndex + i] = (TileMask)this.cellMetas[tilesStartIndex + i].moduleId;
            }
        }
    }



    public delegate void Step3Progress(in MapGenData data);
    public event Step3Progress OnProgress;


    private NativeBitArray          constraints;
    private NativeReference<bool>   hasUncollapsedCells;
    private NativeReference<bool>   hasFailed;
    private NativeReference<bool>   hasSolution;
    private NativeArray<CellMeta>   cellMetas;
    private NativeArray<float>      weights;

    private NativeQueue<int>        minEntropyQueue;
    private NativeReference<int>    minEntropy;
    private NativeArray<int>        stack;

    public override IEnumerator<MapGenStepState> execute(MapGenContext context)
    {
        // initialize module contraints
        this.constraints                            = new NativeBitArray(this.numModules * this.numModules * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var constraintJob                           = new InitializeConstraintsJob
        {
            numModules = this.numModules,
            constraints = constraints
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
            var chunkInfo                           = context.data.mapChunkInfos[chunkId];
            var chunkSize                           = chunkInfo.width * chunkInfo.height;

            // TODO: account for a row/column overlap with previous map chunk

            this.cellMetas                          = new NativeArray<CellMeta>(chunkSize, Allocator.Persistent);
            this.weights                            = new NativeArray<float>(chunkSize * this.numModules, Allocator.Persistent);
            this.stack                              = new NativeArray<int>(chunkSize, Allocator.Persistent);
           
            while(numFailedAttempts < this.settings.numAttemptsToSolveMapChunk)
            {
                // reset cell meta
                cellMetas.CopyFrom(Enumerable.Range(0, chunkSize).Select(_ => CellMeta.Create()).ToArray());

                // set initial weights acording to current map chunk mask
                var initializeWeights               = new InitializeWeightsJob
                {
                    weights                         = weights,
                    mask                            = context.data.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize),
                    numModules                      = this.numModules,
                    maskHeight                      = chunkInfo.height
                }.ScheduleBatch(chunkSize, chunkInfo.width, constraintJob);

                // if all weights zero -> no more work
                hasUncollapsedCells.Value           = true;
                hasFailed.Value                     = false;
                while(!hasFailed.Value && hasUncollapsedCells.Value)
                {
                    var calculateEntropiesJob       = new ComputeEntropiesJob
                    {
                        weights                     = this.weights,
                        cellMetas                   = this.cellMetas,
                        numModules                  = this.numModules
                    }.ScheduleBatch(chunkSize, chunkInfo.width, initializeWeights);

                    var findMinEntropyCellId1        = new FindMinEntropyCellPartialJob
                    {
                        cellMetas                   = this.cellMetas,
                        minEntropy                  = this.minEntropyQueue.AsParallelWriter(),
                    }.ScheduleBatch(this.cellMetas.Length, 1024, calculateEntropiesJob);
                    var findMinEntropyCellId2        = new FindMinEntropyCellJob
                    {
                        cellMetas                   = this.cellMetas,
                        minEntropies                = this.minEntropyQueue,
                        cellId                      = this.minEntropy
                    }.Schedule(findMinEntropyCellId1);

                    var collapseCellJob             = new CollapseCellJob
                    {
                        weights                     = weights,
                        cellId                      = this.minEntropy,
                        cellMetas                   = cellMetas,
                        numModules                  = numModules,
                        rng                         = (float)context.random.NextDouble(),

                        tiles                       = context.data.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize)
                    }.Schedule(findMinEntropyCellId2);

                    var propagateJob                = new PropagateContraintsJob
                    {
                        numModules                  = this.numModules,
                        width                       = chunkInfo.width,
                        height                      = chunkInfo.height,
                        collapsedCellId             = this.minEntropy,
                        cellMetas                   = this.cellMetas,
                        weights                     = this.weights,
                        constraints                 = this.constraints.AsReadOnly(),
                        stack                       = this.stack,
                        hasFailed                   = this.hasFailed
                    }.Schedule(collapseCellJob);

                    // check if all cells are collapsed now
                    this.hasUncollapsedCells.Value  = false;
                    var checkAllCollapsedJob        = new HasUncollapsedJob
                    {
                        cellMetas                   = cellMetas,
                        hasNotCollapsedCells        = hasUncollapsedCells
                    }.Schedule(propagateJob);

                    while(!checkAllCollapsedJob.IsCompleted) { yield return new MapGenStepState {}; }
                    checkAllCollapsedJob.Complete();

                    // report progress
                    this.OnProgress?.Invoke(context.data);
                }

                // check, if all cells have been successfully collapsed
                hasSolution.Value   = true;
                var hasSolutionJob  = new HasSolutionJob
                {
                    cellMetas       = cellMetas,
                    hasSolution     = hasSolution
                }.Schedule();

                while(!hasSolutionJob.IsCompleted) { yield return new MapGenStepState {}; }
                hasSolutionJob.Complete();

                // if we have a solution we are done and can exit loop, else we increase the failed attempt count and try again
                if(hasSolution.Value)
                {
                    //var paintTilesJob   = new PaintTilesJob
                    //{
                    //    tiles           = context.data.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize),
                    //    tilesHeight     = chunkInfo.height,
                    //    cellMetas       = this.cellMetas
                    //}.ScheduleBatch(chunkSize, chunkInfo.width);

                    //while(!paintTilesJob.IsCompleted) { yield return new MapGenStepState {}; }
                    //paintTilesJob.Complete();

                    break;
                }
                else
                {
                    numFailedAttempts++;
                }
            }

            this.weights.Dispose();
            this.cellMetas.Dispose();
            this.stack.Dispose();
        }

        this.hasUncollapsedCells.Dispose();
        this.hasFailed.Dispose();
        this.hasSolution.Dispose();
        this.constraints.Dispose();
        this.minEntropyQueue.Dispose();
        this.minEntropy.Dispose();
    }

    public override void initialize(MapGenContext context)
    {
    }

    public override void release(MapGenContext context)
    {
    }

    public override void Dispose()
    {
        if(this.weights.IsCreated) this.weights.Dispose();
        if(this.cellMetas.IsCreated) this.cellMetas.Dispose();
        if(this.hasUncollapsedCells.IsCreated) this.hasUncollapsedCells.Dispose();
        if(this.hasFailed.IsCreated) this.hasFailed.Dispose();
        if(this.hasSolution.IsCreated) this.hasSolution.Dispose();
        if(this.constraints.IsCreated) this.constraints.Dispose();
        if(this.minEntropyQueue.IsCreated) this.minEntropyQueue.Dispose();
        if(this.minEntropy.IsCreated) this.minEntropy.Dispose();
        if(this.stack.IsCreated) this.stack.Dispose();
    }
}
