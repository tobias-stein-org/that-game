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
        public int      moduleId;
        public float    entropy;
        public bool     isCollapsed;

        public static CellMeta Create()
        {
            return new CellMeta
            {
                isCollapsed = false,
                entropy     = 0.0f,
                moduleId    = -1,
            };
        }

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
        private void allow(int moduleA, int moduleB, ModuleConstraintSide side, int moduleAChunkStart, int moduleBChunkStart, int rowLength)
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
                this.allow(moduleA, moduleB, ModuleConstraintSide.Left, moduleAChunkStart, moduleBChunkStart, rowLength);
                this.allow(moduleA, moduleB, ModuleConstraintSide.Right, moduleAChunkStart, moduleBChunkStart, rowLength);
                this.allow(moduleA, moduleB, ModuleConstraintSide.Top, moduleAChunkStart, moduleBChunkStart, rowLength);
                this.allow(moduleA, moduleB, ModuleConstraintSide.Bottom, moduleAChunkStart, moduleBChunkStart, rowLength);
            }
        }
    }

    /// <summary>
    /// Sets all module weights to zero accoring to the current mapchunk mask.
    /// </summary>
    [BurstCompile]
    private struct ApplyMapChunkMaskJob : IJobParallelForBatch
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
        public NativeArray<CellMeta>    cellMetas;

        [ReadOnly]
        public NativeArray<float>       weights;

        public float                    rng;
        public int                      numModules;
        public NativeReference<int>     cellId;

        //[BurstCompile]
        public void Execute()
        {
            var cellIdValue         = this.cellId.Value;
            // note: cell with lowest entory is always first element
            var meta                = cellMetas[cellIdValue];

            var cellModuleWeights   = weights.Slice(cellIdValue * this.numModules, this.numModules);
            meta.moduleId           = this.pickRandomModule(cellModuleWeights, this.rng * cellModuleWeights.Sum());
            meta.isCollapsed        = true;
            meta.entropy            = 0.0f;

            // update cell meta data in array
            cellMetas[cellIdValue]  = meta;
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

    private NativeBitArray          constraints;
    private NativeReference<bool>   hasUncollapsedCells;
    private NativeReference<bool>   hasSolution;
    private NativeArray<CellMeta>   cellMetas;
    private NativeArray<float>      weights;

    private NativeQueue<int>        minEntropyQueue;
    private NativeReference<int>    minEntropy;

    public override IEnumerator<MapGenStepState> execute(MapGenContext context)
    {
        // initialize module contraints
        this.constraints            = new NativeBitArray(this.numModules * this.numModules * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        var constraintJob           = new InitializeConstraintsJob
        {
            numModules = this.numModules,
            constraints = constraints
        }.Schedule(this.numModules, this.numModules);

        // reusable flags
        this.hasUncollapsedCells    = new NativeReference<bool>(Allocator.Persistent);
        this.hasSolution            = new NativeReference<bool>(Allocator.Persistent);
        this.minEntropyQueue        = new NativeQueue<int>(Allocator.Persistent);
        this.minEntropy             = new NativeReference<int>(Allocator.Persistent);

        for(int chunkId = 0; chunkId < context.data.numMapChunks; chunkId++)
        {
            var numFailedAttempts   = 0;
            var chunkInfo           = context.data.mapChunkInfos[chunkId];
            var chunkSize           = chunkInfo.width * chunkInfo.height;

            // TODO: account for a row/column overlap with previous map chunk

            this.cellMetas          = new NativeArray<CellMeta>(chunkSize, Allocator.Persistent);
            this.weights            = new NativeArray<float>(chunkSize * this.numModules, Allocator.Persistent);

            while(numFailedAttempts < this.settings.numAttemptsToSolveMapChunk)
            {
                // reset cell meta
                cellMetas.CopyFrom(Enumerable.Range(0, chunkSize).Select(_ => CellMeta.Create()).ToArray());

                // set initial weights acording to current map chunk mask
                var applyMaskJob    = new ApplyMapChunkMaskJob
                {
                    weights         = weights,
                    mask            = context.data.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize),
                    numModules      = this.numModules,
                    maskHeight      = chunkInfo.height
                }.ScheduleBatch(chunkSize, chunkInfo.width, constraintJob);

                // init weights according to module contraints
                var initializeWeights = applyMaskJob;


                // if all weights zero -> no more work
                hasUncollapsedCells.Value  = true;
                while(hasUncollapsedCells.Value)
                {

                    var calculateEntropiesJob = initializeWeights;

                    var findMinEntropyPartialJob = new FindMinEntropyCellPartialJob
                    {
                        cellMetas = this.cellMetas,
                        minEntropy = this.minEntropyQueue.AsParallelWriter(),
                    }.ScheduleBatch(this.cellMetas.Length, 1024, calculateEntropiesJob);
 
                    var findMinEntropyJob = new FindMinEntropyCellJob
                    {
                        cellMetas = this.cellMetas,
                        minEntropies = this.minEntropyQueue,
                        cellId = this.minEntropy
                    }.Schedule(findMinEntropyPartialJob);

                    // colapse cell
                    var collapseCellJob = new CollapseCellJob
                    {
                        weights                 = weights,
                        cellId                  = this.minEntropy,
                        cellMetas               = cellMetas,
                        numModules              = numModules,
                        rng                     = (float)context.random.NextDouble()
                    }.Schedule(findMinEntropyJob);
                    
                    // propagate
                    var propagateJob = collapseCellJob;

                    // check if all cells are collapsed now
                    this.hasUncollapsedCells.Value = false;
                    var checkAllCollapsedJob    = new HasUncollapsedJob
                    {
                        cellMetas               = cellMetas,
                        hasNotCollapsedCells    = hasUncollapsedCells
                    }.Schedule(propagateJob);

                    while(!checkAllCollapsedJob.IsCompleted) { yield return new MapGenStepState {}; }
                    checkAllCollapsedJob.Complete();
                }

                // check, if all cells have been successfully collapsed
                {
                    hasSolution.Value   = true;
                    var hasSolutionJob  = new HasSolutionJob
                    {
                        cellMetas       = cellMetas,
                        hasSolution     = hasSolution
                    }.Schedule();

                    while(!hasSolutionJob.IsCompleted) { yield return new MapGenStepState {}; }
                    hasSolutionJob.Complete();
                }

                // if we have a solution we are done and can exit loop, else we increase the failed attempt count and try again
                if(hasSolution.Value)
                {
                    var paintTilesJob   = new PaintTilesJob
                    {
                        tiles           = context.data.walkableTilemapMask.Slice(chunkId * chunkSize, chunkSize),
                        tilesHeight     = chunkInfo.height,
                        cellMetas       = this.cellMetas
                    }.ScheduleBatch(chunkSize, chunkInfo.width);

                    while(!paintTilesJob.IsCompleted) { yield return new MapGenStepState {}; }
                    paintTilesJob.Complete();

                    break;
                }
                else
                {
                    numFailedAttempts++;
                }
            }

            weights.Dispose();
            cellMetas.Dispose();
        }

        hasUncollapsedCells.Dispose();
        hasSolution.Dispose();
        constraints.Dispose();
        minEntropyQueue.Dispose();
        minEntropy.Dispose();
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
        if(this.hasSolution.IsCreated) this.hasSolution.Dispose();
        if(this.constraints.IsCreated) this.constraints.Dispose();
        if(this.minEntropyQueue.IsCreated) this.minEntropyQueue.Dispose();
        if(this.minEntropy.IsCreated) this.minEntropy.Dispose();
    }

}
