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

    public ref MapGenData data { get { return ref this.wrapper.ElementAt(0); } }
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

    public float pipeStart { get; internal set; }
    public float stepStart { get; internal set; }

    /// <summary>
    /// Current map gen step in progress.
    /// </summary>
    internal IEnumerator<MapGenStepState> step { get; set; }

    public static MapGeneratorState CreateInitial()
    {
        return new MapGeneratorState
        {
            state       = State.Initialized,
            step        = null,
            pipeStart   = UnityEngine.Time.time,
            stepStart   = UnityEngine.Time.time,
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


    private readonly IEnumerator<IMapGenStep> steps;
    private MapGenContext mapGenContext;

    public  ref MapGenData data { get { return ref this.mapGenContext.data; } }

    public MapGeneratorState Current { get; private set; } = MapGeneratorState.CreateInitial();

    object IEnumerator.Current => (MapGeneratorState)this.Current;

    internal MapGenerator(in IEnumerator<IMapGenStep> steps)
    {
        this.steps = steps;
        this.mapGenContext = MapGenContext.Create();
    }

    public void Dispose()
    {
        // allow each map gen step to release its previously initialized resources.
        this.steps.Reset();
        while(this.steps.MoveNext()) { this.steps.Current.release(this.mapGenContext); }

        this.mapGenContext.Dispose();
    }

    public bool MoveNext()
    {
        switch(this.Current.state)
        {
            case MapGeneratorState.State.Initialized:
            {
                this.Current.pipeStart = UnityEngine.Time.time;

                this.OnMapGenStarted?.Invoke();

                // initialize map generate state

                // move to first pipeline step
                if(this.steps.MoveNext())
                {
                    this.steps.Current.initialize(this.mapGenContext);
                    this.Current.step = this.steps.Current.execute(this.mapGenContext);

                    this.Current.stepStart = UnityEngine.Time.time;
                    this.OnMapGenStepStarted?.Invoke(this.steps.Current, this.mapGenContext.data);
                }

                this.Current.state = this.steps.Current != null
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
                    if(this.Current.step.MoveNext())
                    {
                        var stepState = this.Current.step.Current;
                        this.OnMapGenStepUpdated?.Invoke(this.steps.Current, this.mapGenContext.data);
                    }
                    // map gen step completed
                    else
                    {
                        var duration = UnityEngine.Time.time - this.Current.stepStart;
                        Debug.Log($"Map generator step {this.steps.Current} finished in: {TimeSpan.FromSeconds(duration).ToString()}");

                        this.OnMapGenStepFinished?.Invoke(this.steps.Current, this.mapGenContext.data);

                        // move on to next step in the pipeline
                        if(this.steps.MoveNext())
                        {
                            this.steps.Current.initialize(this.mapGenContext);
                            this.Current.step = this.steps.Current.execute(this.mapGenContext);

                            this.Current.stepStart = UnityEngine.Time.time;
                            this.OnMapGenStepStarted?.Invoke(this.steps.Current, this.mapGenContext.data);
                        }
                        // no more steps to process
                        else
                        {
                            this.Current.step = null;
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
                var duration = UnityEngine.Time.time - this.Current.pipeStart;
                Debug.Log($"Map generator finished in: {TimeSpan.FromSeconds(duration).ToString()}");

                this.OnMapGenFinished?.Invoke(this.mapGenContext.data);
                break;
            }
        }

        return false;
    }

    public void Reset()
    {
        this.Dispose();

        // create new context
        this.mapGenContext  = MapGenContext.Create();

        // reset mep generator state
        this.Current        = MapGeneratorState.CreateInitial();
        this.steps.Reset();

        this.OnMapGenReseted?.Invoke();
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
        //this.executor.OnMapGenStepUpdated += OnMapGenStepFinished;
        this.executor.OnMapGenStepFinished += OnMapGenStepFinished;
        this.executor.OnMapGenReseted += () => this.done = false;
        this.executor.OnMapGenFinished += (in MapGenData data) => this.done = true;

        this.reset();
    }

    private void OnMapGenStepStarted(in IMapGenStep step, in MapGenData data)
    {
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
        else if(typeof(Step2) == step.GetType())
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
                        var mask = chunk[i];
                        //var mask = data.walkableTilemapMask[c][i];
                        Vector3Int tp = new Vector3Int(x + p.x, y + p.y, 0);
                        tilemap.SetTile(tp, mask == TileMask.Wall ? this.wall : this.tile);
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
