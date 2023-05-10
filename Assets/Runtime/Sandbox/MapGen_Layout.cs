using UnityEngine;
using UnityEngine.Tilemaps;

using Unity.Jobs;
using Unity.Collections;

using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Linq;
using System.Collections;
using System;

public partial struct MapGenData
{
}

public class MapGenContext : System.IDisposable
{
    private NativeList<MapGenData> wrapper;

    private System.Random rng;

    internal static MapGenContext Create(int seed = 0)
    {
        var wrapper = new NativeList<MapGenData>(1, Allocator.Persistent);
        wrapper.AddNoResize(new MapGenData());

        return new MapGenContext
        {
            wrapper = wrapper,
            rng = seed != 0 ? new System.Random(seed) : new System.Random()
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

    public ref MapGenData data { get { return ref this.wrapper.ElementAt(0); } }

    public System.Random random { get { return this.rng; } }
}

public class MapGenStepState
{

}

public class MapGeneratorState
{
    public enum State
    {
        Initialized,
        Running,
        Completed
    }

    public State state { get; internal set; }

    public DateTime pipeStart { get; internal set; }
    public DateTime stepStart { get; internal set; }

    internal IEnumerator<IMapGenStep> pipe { get; private set; }

    /// <summary>
    /// Current map gen step state.
    /// </summary>
    internal IEnumerator<MapGenStepState> step { get; set; }

    public static MapGeneratorState CreateInitial(IEnumerable<IMapGenStep> pipe)
    {
        return new MapGeneratorState
        {
            state       = State.Initialized,
            pipe        = pipe.GetEnumerator(),
            step        = null,
            pipeStart   = DateTime.Now,
            stepStart   = DateTime.Now,
        };
    }
}

public interface IMapGenStep : System.IDisposable
{
    IEnumerator<MapGenStepState> execute(MapGenContext context);
    void initialize(MapGenContext context);
    void release(MapGenContext context);
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
   
    public abstract IEnumerator<MapGenStepState> execute(MapGenContext context);
    public abstract void initialize(MapGenContext context);
    public abstract void release(MapGenContext context);

    public abstract void Dispose();
}

public class MapGenPipeline : System.IDisposable, IEnumerable<IMapGenStep>
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

    public IReadOnlyList<IMapGenStep> getSteps { get { return this.steps; } }

    public static MapGenPipelineBuilder create() { return new MapGenPipelineBuilder(); }

    public MapGenerator createExecutor() { return new MapGenerator(this); }

    public IEnumerator<IMapGenStep> GetEnumerator() { return this.steps.GetEnumerator(); }

    IEnumerator IEnumerable.GetEnumerator() { return this.steps.GetEnumerator(); }
}

public class MapGenPipelineBuilder
{
    internal readonly List<IMapGenStep> steps = new List<IMapGenStep>();

    public MapGenPipelineBuilder add<TStep, TStepSettings>(in TStepSettings settings = null)
        where TStep : MapGenStep<TStepSettings>, new()
        where TStepSettings : MapGenStepSettings
    {
        return this.add(new TStep(), settings);
    }

    public MapGenPipelineBuilder add<TStep, TStepSettings>(TStep step, TStepSettings settings = null)
        where TStep : MapGenStep<TStepSettings>, new()
        where TStepSettings : MapGenStepSettings
    {
        if(settings)
        {
            step.configure(settings);
        }

        this.steps.Add(step);

        return this;
    }

    public MapGenPipeline build() { return new MapGenPipeline(this.steps); }
}


public class MapGenerator : IEnumerator<MapGeneratorState>
{
    public delegate void OnMapGenStepStart(in IMapGenStep step, in MapGenData mapGenData);
    public delegate void OnMapGenStepUpdate(in IMapGenStep step, in MapGenData mapGenData);
    public delegate void OnMapGenStepFinish(in IMapGenStep step, in MapGenData mapGenData);
    public delegate void OnMapGenReset();
    public delegate void OnMapGenStart();
    public delegate void OnMapGenFinish(in MapGenData mapGenData);

    public event OnMapGenStepStart  OnMapGenStepStarted;
    public event OnMapGenStepUpdate OnMapGenStepUpdated;
    public event OnMapGenStepFinish OnMapGenStepFinished;
    public event OnMapGenReset      OnMapGenReseted;
    public event OnMapGenStart      OnMapGenStarted;
    public event OnMapGenFinish     OnMapGenFinished;


    private readonly MapGenPipeline pipeline;
    private MapGenContext mapGenContext;

    public  ref MapGenData data { get { return ref this.mapGenContext.data; } }

    public MapGeneratorState Current { get; private set; } = null;

    object IEnumerator.Current => (MapGeneratorState)this.Current;

    internal MapGenerator(in MapGenPipeline pipeline)
    {
        this.pipeline = pipeline;
        this.mapGenContext = MapGenContext.Create();
    }

    public bool MoveNext()
    {
        switch(this.Current.state)
        {
            case MapGeneratorState.State.Initialized:
            {
                this.Current.pipeStart = DateTime.Now;

                this.OnMapGenStarted?.Invoke();

                // initialize map generate state

                // move to first pipeline step
                if(this.Current.pipe.MoveNext())
                {
                    this.Current.pipe.Current.initialize(this.mapGenContext);
                    this.Current.step = this.Current.pipe.Current.execute(this.mapGenContext);

                    this.Current.stepStart = DateTime.Now;
                    this.OnMapGenStepStarted?.Invoke(this.Current.pipe.Current, this.mapGenContext.data);
                }

                this.Current.state = this.Current.pipe.Current != null
                        ? MapGeneratorState.State.Running
                        : MapGeneratorState.State.Completed;

                return true;
            }

            case MapGeneratorState.State.Running:
            {
                // update current map gen step
                if(this.Current.step != null)
                {
                    // update current pipeline step
                    var maxSubStepDuration = TimeSpan.FromMilliseconds(5);
                    var t0 = System.DateTime.Now;
                    while(System.DateTime.Now - t0 < maxSubStepDuration)
                    {
                        if(this.Current.step.MoveNext())
                        {
                            var stepState = this.Current.step.Current;
                            this.OnMapGenStepUpdated?.Invoke(this.Current.pipe.Current, this.mapGenContext.data);
                        }
                        // map gen step completed
                        else
                        {
                            var duration = DateTime.Now - this.Current.stepStart;
                            Debug.Log($"Map generator step {this.Current.pipe.Current} finished in: {duration}");

                            this.OnMapGenStepFinished?.Invoke(this.Current.pipe.Current, this.mapGenContext.data);

                            // move on to next step in the pipeline
                            if(this.Current.pipe.MoveNext())
                            {
                                this.Current.pipe.Current.initialize(this.mapGenContext);
                                this.Current.step = this.Current.pipe.Current.execute(this.mapGenContext);

                                this.Current.stepStart = DateTime.Now;
                                this.OnMapGenStepStarted?.Invoke(this.Current.pipe.Current, this.mapGenContext.data);
                            }
                            // no more steps to process
                            else
                            {
                                this.Current.step = null;
                            }

                            // force exit sub-step loop
                            break;
                        }
                    }
                }
                // if there are no steps to process, set map generator state to complete.
                else
                {
                    this.Current.state = MapGeneratorState.State.Completed;
                }

                return true;
            }

            case MapGeneratorState.State.Completed:
            {
                var duration = DateTime.Now - this.Current.pipeStart;
                Debug.Log($"Map generator finished in: {duration}");

                this.OnMapGenFinished?.Invoke(this.mapGenContext.data);
                break;
            }
        }

        return false;
    }

    public void Reset()
    {
        // allow each map gen step to release its previously initialized resources.
        foreach(var step in this.pipeline.getSteps) { step.release(this.mapGenContext); }

        // create new context
        this.mapGenContext  = MapGenContext.Create();

        // reset mep generator state
        this.Current        = MapGeneratorState.CreateInitial(this.pipeline);

        this.OnMapGenReseted?.Invoke();
    }

    public void Dispose()
    {
        // allow each map gen step to release its previously initialized resources.
        foreach(var step in this.pipeline.getSteps) { step.release(this.mapGenContext); }

        this.pipeline.Dispose();
        this.mapGenContext.Dispose();
    }
}


public class MapGen_Layout : MonoBehaviour
{
    public Tilemap          tilemap;
    public Tile             tile;
    public Tile             wall;
    public Tile             path;

    public Step1Settings    step1Settings;
    public Step2Settings    step2Settings;
    public Step3Settings    step3Settings;

    private MapGenerator    executor;

    public bool done = false;

    // Start is called before the first frame update
    void Start()
    {
        var step3 = new Step3();
        //step3.OnProgress += OnStep3Progress;

        var pipeline = MapGenPipeline
                .create()
                    .add<Step1, Step1Settings>(this.step1Settings)
                    .add<Step2, Step2Settings>(this.step2Settings)
                    //.add<Step3, Step3Settings>(this.step3Settings)
                    .add(step3, this.step3Settings)
                .build();

        this.executor = pipeline.createExecutor();

        this.executor.OnMapGenStepStarted += OnMapGenStepStarted;
        //this.executor.OnMapGenStepUpdated += OnMapGenStepFinished;
        this.executor.OnMapGenStepFinished += OnMapGenStepFinished;
        this.executor.OnMapGenReseted += () => this.done = false;
        this.executor.OnMapGenFinished += (in MapGenData data) => this.done = true;

        this.reset();
    }

    private void OnMapGenStepStarted(in IMapGenStep step, in MapGenData data)
    {

    }

    private void OnStep3Progress(in MapGenData data)
    {
        tilemap.ClearAllTiles();

        Vector2Int p = this.step1Settings.pathStart;


        var chunkSize = this.step2Settings.mapChunkDimensions.x * this.step2Settings.mapChunkDimensions.y;

        for(int c = 0; c < data.pathSteps.Length + 1; c++)
        {
            var chunk = data.walkableTilemapMask.Slice(c * chunkSize, chunkSize);
            for(int y = 0; y < this.step2Settings.mapChunkDimensions.y; y++)
            {
                for(int x = 0; x < this.step2Settings.mapChunkDimensions.x; x++)
                {
                    int i = (y * this.step2Settings.mapChunkDimensions.x) + x;
                    Vector3Int tp = new Vector3Int(x + p.x, y + p.y, 0);

                    switch(chunk[i])
                    {
                        case TileMask.Wall: this.tilemap.SetTile(tp, this.wall); break;
                        case TileMask.Walkable: this.tilemap.SetTile(tp, this.path); break;

                        default:
                            this.tilemap.SetTile(tp, this.tile); break;
                    }
                }
            }

            if(c < data.pathSteps.Length)
            {
                p += new Vector2Int(
                    data.pathSteps[c].x * this.step2Settings.mapChunkDimensions.x,
                    data.pathSteps[c].y * this.step2Settings.mapChunkDimensions.y);
            }
        }
    }

    private void OnMapGenStepFinished(in IMapGenStep step, in MapGenData data)
    {
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
        else if(typeof(Step2) == step.GetType() || typeof(Step3) == step.GetType())
        {
            tilemap.ClearAllTiles();

            Vector2Int p = this.step1Settings.pathStart;


            var chunkSize = this.step2Settings.mapChunkDimensions.x * this.step2Settings.mapChunkDimensions.y;

            for(int c = 0; c < data.pathSteps.Length + 1; c++)
            {
                var chunk = data.walkableTilemapMask.Slice(c * chunkSize, chunkSize);
                for(int y = 0; y < this.step2Settings.mapChunkDimensions.y; y++)
                {
                    for(int x = 0; x < this.step2Settings.mapChunkDimensions.x; x++)
                    {
                        int i = (y * this.step2Settings.mapChunkDimensions.x) + x;
                        Vector3Int tp = new Vector3Int(x + p.x, y + p.y, 0);

                        switch(chunk[i])
                        {
                            case TileMask.Wall: this.tilemap.SetTile(tp, this.wall); break;
                            case TileMask.Walkable: this.tilemap.SetTile(tp, this.path); break;

                            default:
                                this.tilemap.SetTile(tp, this.tile); break;
                        }
                    }
                }

                if(c < data.pathSteps.Length)
                {
                    p += new Vector2Int(
                        data.pathSteps[c].x * this.step2Settings.mapChunkDimensions.x,
                        data.pathSteps[c].y * this.step2Settings.mapChunkDimensions.y);
                }
            }
        }
    }

    private void OnDestroy()
    {
        this.tilemap?.ClearAllTiles();
        this.executor?.Dispose();
    }

    public void reset()
    {
        this.executor.Reset();
        StartCoroutine(this.executor);
    }
}
