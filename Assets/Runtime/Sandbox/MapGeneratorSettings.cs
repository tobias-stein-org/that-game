using System;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Generator Settings")]
public class MapGeneratorSettings : ScriptableObject
{
    public  int             seed    = 0;

    /// <summary>
    /// Called at runtime to initialize the runtime settings.
    /// </summary>
    public void initialize()
    {
        this.random         = this.seed != 0 ? new System.Random(this.seed) : new System.Random();
        this.pendingJobs    = new Queue<JobHandle>(64);
    }


    public static MapGeneratorSettings Default { get { return ScriptableObject.CreateInstance<MapGeneratorSettings>(); } }


    #region Runtime Only

    public System.Random    random  { get; private set; }

    public bool hasPendingJobs
    {
        get
        {
            while(this.pendingJobs.Count > 0 && this.pendingJobs.Peek().IsCompleted)
            {
                this.pendingJobs.Dequeue().Complete();
            }
    
            return this.pendingJobs.Count > 0;
        }
    }
    
    public void awaitPendingJobs()
    {
        while(this.pendingJobs.TryDequeue(out JobHandle jobHandle))
        {
            jobHandle.Complete();
        }
    }

    public JobHandle schedule<T>(T job, JobHandle dependsOn = default)
        where T: struct, IJob
    {
        var handle = IJobExtensions.Schedule(job, dependsOn);
        this.pendingJobs.Enqueue(handle);

        return handle;
    }

    public JobHandle schedule<T>(T job, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
        where T: struct, IJobParallelFor
    {
        var handle = IJobParallelForExtensions.Schedule(job, arrayLength, innerloopBatchCount, dependsOn);
        this.pendingJobs.Enqueue(handle);

        return handle;
    }

    public JobHandle scheduleBatch<T>(T job, int arrayLength, int innerloopBatchCount, JobHandle dependsOn = default)
        where T: struct, IJobParallelForBatch
    {
        var handle = IJobParallelForBatchExtensions.ScheduleBatch(job, arrayLength, innerloopBatchCount, dependsOn);
        this.pendingJobs.Enqueue(handle);

        return handle;
    }

    /// <summary>
    /// Unmanaged data (no garbage collection). Pipeline steps are allocating and freeing resources.
    /// </summary>
    [NonSerialized]
    private MapGenData          unmanaged;

    public  ref MapGenData      data { get { return ref this.unmanaged; } }

    [NonReorderable]
    private Queue<JobHandle>    pendingJobs;

    #endregion // Runtime Only
}
