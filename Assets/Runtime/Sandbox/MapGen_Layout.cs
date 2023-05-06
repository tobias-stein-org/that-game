using UnityEngine;
using UnityEngine.Tilemaps;

using Unity.Jobs;
using Unity.Collections;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;

public partial struct MapGenData
{
}

public struct MapGenContext : System.IDisposable
{
    private NativeList<MapGenData> wrapper;

    internal static MapGenContext Create()
    {
        var wrapper = new NativeList<MapGenData>(1, Allocator.Persistent);
        wrapper.AddNoResize(new MapGenData());

        return new MapGenContext
        {
            wrapper = wrapper
        };
    }

    public void Dispose()
    {
        if(this.wrapper.IsCreated)
        {
            Debug.Log("Dispose map-gen context.");
            this.wrapper.Dispose();
        }
    }

    public readonly ref MapGenData data { get { return ref this.wrapper.ElementAt(0); } }
}

public interface IMapGenStep : System.IDisposable
{
    void initialize(ref MapGenContext context);
    void execute(ref MapGenContext context);
    void release(ref MapGenContext context);

}

public class MapGenStepSettings: ScriptableObject
{
}

public abstract class MapGenStep<TStepSettings> : IMapGenStep
{
    protected TStepSettings settings;

    public MapGenStep()
    {}

    public virtual void configure(in TStepSettings settings)
    {
        this.settings = settings;
    }

   
    public abstract void initialize(ref MapGenContext context);
    public abstract void execute(ref MapGenContext context);
    public abstract void release(ref MapGenContext context);

    public abstract void Dispose();
}

public class MapGenPipeline : System.IDisposable
{
    private readonly List<IMapGenStep> steps;

    internal MapGenPipeline(in List<IMapGenStep> steps)
    {
        this.steps = steps;
    }

    public void Dispose()
    {
        Debug.Log("Dispose map-gen pipeline.");
        this.steps.ForEach(step =>
        {
            Debug.Log($"Dispose map-gen pipeline step {step.GetType()}.");
            step.Dispose();
        });

        this.steps.Clear();
    }

    public static MapGenPipelineBuilder create()
    {
        return new MapGenPipelineBuilder();
    }

    public MapGenerator createExecutor()
    {
        return new MapGenerator(this.steps.GetEnumerator());
    }
}

public class MapGenPipelineBuilder
{
    internal readonly List<IMapGenStep> steps = new List<IMapGenStep>();

    public MapGenPipelineBuilder add<TStep, TStepSettings>(in TStepSettings settings)
        where TStep : MapGenStep<TStepSettings>, new()
        where TStepSettings : MapGenStepSettings
    {
        // create new instance of step
        var newStep = new TStep();
        // configure step
        newStep.configure(settings);
        // add step to pipeline
        this.steps.Add(newStep);

        return this;
    }

    public MapGenPipeline build() { return new MapGenPipeline(this.steps); }
}


public class MapGenerator : System.IDisposable
{
    public delegate void OnMapGenStepStart(in IMapGenStep step, in MapGenData mapGenData);
    public delegate void OnMapGenStepFinish(in IMapGenStep step, in MapGenData mapGenData);
    public delegate void OnMapGenReset();
    public delegate void OnMapGenFinish(in MapGenData mapGenData);

    public event OnMapGenStepStart  OnMapGenStepStarted;
    public event OnMapGenStepFinish OnMapGenStepFinished;
    public event OnMapGenReset      OnMapGenReseted;
    public event OnMapGenFinish     OnMapGenFinished;

    internal struct ExecutionContext : System.IDisposable
    {
        private GCHandle                memHandle;

        public IMapGenStep              step;
        public MapGenContext            mapGenContext;

        public static implicit operator GCHandle(ExecutionContext ctx) => ctx.memHandle;

        public static ExecutionContext Create(IMapGenStep step, MapGenContext mapGenContext)
        {
            var instance = new ExecutionContext
            {
                step            = step,
                mapGenContext   = mapGenContext
            };

            instance.memHandle  = GCHandle.Alloc(instance);
            return instance;
        }

        public void Dispose()
        {
            if(this.memHandle.IsAllocated) { this.memHandle.Free(); }
        }
    }

    private struct MapGenJob : IJob
    {
        public GCHandle executionContext;

        public void Execute()
        {
            var executionContext = (ExecutionContext)this.executionContext.Target;
            executionContext.step.execute(ref executionContext.mapGenContext);
        }
    }

    private readonly IEnumerator<IMapGenStep> steps;
    private JobHandle jobHandle;

    private MapGenContext mapGenContext;
    private ExecutionContext exeContext;

    public  ref MapGenData data { get { return ref this.mapGenContext.data; } }

    internal MapGenerator(in IEnumerator<IMapGenStep> steps)
    {
        this.steps = steps;
        this.mapGenContext = MapGenContext.Create();
    }

    public void update()
    {
        // is previous step completed?
        if(this.jobHandle.IsCompleted)
        {
            // any previous step?
            if(this.steps.Current != null)
            {
                this.OnMapGenStepFinished?.Invoke(this.steps.Current, this.mapGenContext.data);
            }

            // continue with next step
            if(this.steps.MoveNext())
            {
                var nextStep            = this.steps.Current;
                nextStep.initialize(ref this.mapGenContext);

                // dispose old and create new execution context
                this.exeContext.Dispose();
                this.exeContext = ExecutionContext.Create(nextStep, this.mapGenContext);

                // schedule step to be run immediately
                this.OnMapGenStepStarted?.Invoke(nextStep, this.mapGenContext.data);
                this.jobHandle = new MapGenJob { executionContext = this.exeContext }.Schedule();
            }
            else
            {
                // no more steps to process
                this.OnMapGenFinished?.Invoke(this.mapGenContext.data);
            }
        }
    }

    public void reset()
    {
        this.Dispose();

        this.mapGenContext = MapGenContext.Create();

        this.steps.Reset();
        this.OnMapGenReseted?.Invoke();
    }

    public void Dispose()
    {
        this.exeContext.Dispose();

        // allow each map gen step to release its previously initialized resources.
        this.steps.Reset();
        while(this.steps.MoveNext()) { this.steps.Current.release(ref this.mapGenContext); }

        this.mapGenContext.Dispose();
    }
}


public class MapGen_Layout : MonoBehaviour
{
    public Tilemap          tilemap;
    public Tile             tile;
    public Tile             wall;

    public Step1Settings    step1Settings;
    public Step2Settings    step2Settings;

    private MapGenerator    executor;

    public bool done = false;

    // Start is called before the first frame update
    void Start()
    {
        var pipeline = MapGenPipeline
                .create()
                    .add<Step1, Step1Settings>(this.step1Settings)
                    .add<Step2, Step2Settings>(this.step2Settings)
                .build();

        this.executor = pipeline.createExecutor();

        this.executor.OnMapGenStepStarted += OnMapGenStepStarted;
        this.executor.OnMapGenStepFinished += OnMapGenStepFinished;
        this.executor.OnMapGenReseted += () => this.done = false;
        this.executor.OnMapGenFinished += (in MapGenData data) => this.done = true;
    }

    private void OnMapGenStepStarted(in IMapGenStep step, in MapGenData data)
    {
        Debug.Log($"{step.GetType()} started");
    }

    private void OnMapGenStepFinished(in IMapGenStep step, in MapGenData data)
    {
        Debug.Log($"{step.GetType()} finsihed");

        if(typeof(Step1) == step.GetType())
        {
            tilemap.ClearAllTiles();

            Vector2Int s = this.step1Settings.pathStart;

            // start tile
            tilemap.SetTile((Vector3Int)s, this.tile);

            // path
            for(int i = 1; i < data.pathSteps.Length - 1; i++)
            {
                s += data.pathSteps[i];
                tilemap.SetTile((Vector3Int)s, this.tile);
            }

            // end tile
            if(data.pathSteps.Length > 1)
            {
                s += data.pathSteps[data.pathSteps.Length - 1];

                tilemap.SetTile((Vector3Int)s, this.tile);
            }
        }
        else if(typeof(Step2) == step.GetType())
        {
            tilemap.ClearAllTiles();

            Vector2Int p = this.step1Settings.pathStart;


            var chunkSize = this.step2Settings.mapChunckDimensions.x * this.step2Settings.mapChunckDimensions.y;

            for(int c = 0; c < data.pathSteps.Length + 1; c++)
            {
                for(int y = 0; y < this.step2Settings.mapChunckDimensions.y; y++)
                {
                    for(int x = 0; x < this.step2Settings.mapChunckDimensions.x; x++)
                    {
                        int i = (y * this.step2Settings.mapChunckDimensions.x) + x;
                        var mask = data.walkableTilemapMask[c][i];
                        Vector3Int tp = new Vector3Int(x + p.x, y + p.y, 0);
                        tilemap.SetTile(tp, mask == TileMask.Wall ? this.wall : this.tile);
                    }
                }

                if(c < data.pathSteps.Length)
                {
                    p += new Vector2Int(
                        data.pathSteps[c].x * this.step2Settings.mapChunckDimensions.x,
                        data.pathSteps[c].y * this.step2Settings.mapChunckDimensions.y);
                }
            }
        }
    }

    private void OnDestroy()
    {
        this.tilemap?.ClearAllTiles();
        this.executor?.Dispose();
    }


    // Update is called once per frame
    void Update()
    {
        if(!this.done)
        {
            this.executor.update();
        }
    }

    public void reset()
    {
        this.executor.reset();
    }
}
