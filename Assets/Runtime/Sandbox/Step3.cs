using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

public partial struct MapGenData
{

}

public class Step3 : MapGenStep<Step3Settings>
{
    private int numModules = 3;

    [BurstCompile]
    private struct CellMeta
    {
        public int      moduleId;
        public float    entropy;
        public bool     isCollapsed;

        public static CellMeta Empty
        {
            get
            {
                return new CellMeta
                {
                    isCollapsed = false,
                    entropy     = 0.0f,
                    moduleId    = -1,
                };
            }
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
    private struct ApplyMapChunkMaskJob : IJobParallelFor
    {
        public int                      numModules;
        public int                      numColumns;

        [NativeDisableParallelForRestriction]
        public NativeArray<float>       weights;

        [ReadOnly]
        public NativeArray<TileMask>    mask;

        public void Execute(int row)
        {
            var chunkSize   = this.numColumns;
            var chunkStart  = row * chunkSize;

            for(int i = chunkStart; i < chunkStart + chunkSize; i++)
            {
                for(int moduleId = 0; moduleId < this.numModules; moduleId++)
                {
                    this.weights[i + moduleId] = 1.0f;
                }
            }
        }
    }

    [BurstCompile]
    private struct CheckAllCollapsedJob : IJob
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
    private struct CheckSolutionJob : IJob
    {
        [ReadOnly]
        public NativeArray<CellMeta>    cellMetas;

        public NativeReference<bool>    hasSolution;

        [BurstCompile]
        public void Execute()
        {
            for(int i = 0; i < (this.cellMetas.Length); i++)
            {
                if(this.cellMetas[i].moduleId == -1) { this.hasSolution.Value = false; break; }
            }
        }
    }

    public override IEnumerator<MapGenStepState> execute(MapGenContext context)
    {
        // initialize module contraints
        var constraints     = new NativeBitArray(this.numModules * this.numModules * 4, Allocator.Persistent, NativeArrayOptions.ClearMemory);
        {
            var constraintJob = new InitializeConstraintsJob
            {
                numModules = this.numModules,
                constraints = constraints
            }.Schedule(this.numModules, 16);

            while(!constraintJob.IsCompleted) { yield return new MapGenStepState { }; }
            constraintJob.Complete();
        }


        // reusable flags
        var hasNotCollapsedCells    = new NativeReference<bool>(Allocator.Persistent);
        var hasSolution             = new NativeReference<bool>(Allocator.Persistent);

    
        for(int chunkId = 0; chunkId < context.data.numMapChunks; chunkId++)
        {
            var numFailedAttempts   = 0;
            var chunkInfo           = context.data.mapChunkInfos[chunkId];
            var chunkSize           = chunkInfo.width * chunkInfo.height;

            // TODO: account for a row/column overlap with previous map chunk

            var cellMetas    = new NativeArray<CellMeta>(chunkSize, Allocator.Persistent);
            var weights     = new NativeArray<float>(chunkSize * this.numModules, Allocator.Persistent);

            while(numFailedAttempts < this.settings.numAttemptsToSolveMapChunk)
            {

                // reset cell meta
                cellMetas.CopyFrom(Enumerable.Range(0, chunkSize).Select(_ => CellMeta.Empty).ToArray());

                // set initial weights acording to current map chunk mask
                {
                    var applyMaskJob    = new ApplyMapChunkMaskJob
                    {
                        numModules      = this.numModules,
                        numColumns      = chunkInfo.width,
                        weights         = weights,
                        mask            = context.data.walkableTilemapMask
                    }.Schedule(chunkInfo.height, 16);
                    while(!applyMaskJob.IsCompleted) { yield return new MapGenStepState {}; }
                    applyMaskJob.Complete();
                }

                // init weights according to module contraints


                // check weights after propagation is complete
                {
                    hasNotCollapsedCells.Value  = false;
                    var checkWeightsJob         = new CheckAllCollapsedJob
                    {
                        cellMetas               = cellMetas,
                        hasNotCollapsedCells    = hasNotCollapsedCells
                    }.Schedule();

                    while(!checkWeightsJob.IsCompleted) { yield return new MapGenStepState {}; }
                    checkWeightsJob.Complete();
                }

                // if all weights zero -> no more work
                while(hasNotCollapsedCells.Value)
                {

                    // calc entropies

                    // find cell with lowest entropy
                    var t0 = System.DateTime.Now;

                    int     cellId          = -1;
                    float   lowestEntropy   = float.MaxValue;
                    for(int i = 0; i < cellMetas.Length; i++)
                    {
                        if(!cellMetas[i].isCollapsed && cellMetas[i].entropy < lowestEntropy)
                        {
                            cellId          = i;
                            lowestEntropy   = cellMetas[i].entropy;
                        }
                    }
                    Debug.Log((System.DateTime.Now - t0).TotalMilliseconds);

        

                    // colapse cell
                    {
                        var meta                = cellMetas[cellId];
                        var cellModuleWeights   = weights.Slice(cellId * this.numModules, this.numModules);
                        meta.moduleId           = this.pickRandomModule(cellModuleWeights, (float)context.random.NextDouble() * cellModuleWeights.Sum());
                        meta.isCollapsed        = true;
                        meta.entropy            = 0.0f;

                        // update cell meta data in array
                        cellMetas[cellId] = meta;
                    }
                    
                    // propagate

                    // check if all cells are collapsed now
                    {
                        hasNotCollapsedCells.Value = false;
                        var checkAllCollapsed = new CheckAllCollapsedJob
                        {
                            cellMetas = cellMetas,
                            hasNotCollapsedCells = hasNotCollapsedCells
                        }.Schedule();
                        while(!checkAllCollapsed.IsCompleted) { yield return new MapGenStepState {}; }
                        checkAllCollapsed.Complete();
                    }
                }

                // check, if all cells have been successfully collapsed
                {
                    hasSolution.Value   = true;
                    var hasSolutionJob  = new CheckSolutionJob
                    {
                        cellMetas       = cellMetas,
                        hasSolution     = hasSolution
                    }.Schedule();

                    while(!hasSolutionJob.IsCompleted) { yield return new MapGenStepState {}; }
                    hasSolutionJob.Complete();
                }

                // if we have a solution we are done and can exit loop, else we increase the failed attempt count and try again
                if(hasSolution.Value) { break; } else { numFailedAttempts++; }
            }

            weights.Dispose();
            cellMetas.Dispose();
        }

        hasNotCollapsedCells.Dispose();
        hasSolution.Dispose();
        constraints.Dispose();
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

    public override void initialize(MapGenContext context)
    {
    }

    public override void release(MapGenContext context)
    {
    }

    public override void Dispose()
    {
    }

}
