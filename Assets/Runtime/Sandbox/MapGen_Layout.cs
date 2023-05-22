using UnityEngine;
using UnityEngine.Tilemaps;

using System.Collections.Generic;
using System.Linq;
using System.Collections;
using System;

public partial struct MapGenData
{
    public struct Layer : IEquatable<Layer>
    {
        private readonly int layerIndex;

        public static int MAX_LAYERS        { get; private set; } = 0;
        private Layer(int index)
        {
            this.layerIndex = index;
            Layer.MAX_LAYERS++;
        }

        public override int GetHashCode() { return this.layerIndex.GetHashCode(); }

        public bool Equals(Layer other) { return this.layerIndex == other.layerIndex; }

        public static implicit operator int(Layer layer) { return layer.layerIndex; }

        public static Layer Floor           = new Layer(0);
        public static Layer Obstructable    = new Layer(1);
    }
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
    void configure(in MapGenStepSettingsBase settings);

    IEnumerator<MapGenStepState> execute(MapGeneratorSettings context);
    void initialize(MapGeneratorSettings context);
    void release(MapGeneratorSettings context);
}

public abstract class MapGenStepSettingsBase : ScriptableObject
{
    public abstract System.Type MapGenStepType { get; }
}

public class MapGenStepSettings<TStep> : MapGenStepSettingsBase
{
    public override System.Type MapGenStepType { get { return typeof(TStep); } }
}

public abstract class MapGenStep<TStepSettings> : IMapGenStep
    where TStepSettings : MapGenStepSettingsBase
{
    protected TStepSettings settings;

    public MapGenStep()
    {}

    public virtual void configure(in MapGenStepSettingsBase settings)
    {
        this.settings = (TStepSettings)settings;
    }
   
    public abstract IEnumerator<MapGenStepState> execute(MapGeneratorSettings context);
    public abstract void initialize(MapGeneratorSettings context);
    public abstract void release(MapGeneratorSettings context);

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
        // note: we start releasing the latest steps first as they might rely on resources from previous steps
        
        foreach(var step in this.getSteps.Reverse())
        {
            Debug.Log($"Dispose map-gen pipeline step {step.GetType()}.");
            step.Dispose();
        }

        this.steps.Clear();
    }

    public IReadOnlyList<IMapGenStep> getSteps { get { return this.steps; } }

    public static MapGenPipelineBuilder create() { return new MapGenPipelineBuilder(); }

    public MapGenerator createExecutor(MapGeneratorSettings settings) { return new MapGenerator(this, settings); }

    public IEnumerator<IMapGenStep> GetEnumerator() { return this.steps.GetEnumerator(); }

    IEnumerator IEnumerable.GetEnumerator() { return this.steps.GetEnumerator(); }
}

public class MapGenPipelineBuilder
{
    internal readonly List<IMapGenStep> steps = new List<IMapGenStep>();

    public MapGenPipelineBuilder add<TStep, TStepSettings>(in TStepSettings settings = null)
        where TStep : MapGenStep<TStepSettings>, new()
        where TStepSettings : MapGenStepSettingsBase
    {
        return this.add(new TStep(), settings);
    }

    public MapGenPipelineBuilder add<TStep, TStepSettings>(TStep step, TStepSettings settings = null)
        where TStep : MapGenStep<TStepSettings>, new()
        where TStepSettings : MapGenStepSettingsBase
    {
        if(settings)
        {
            step.configure(settings);
        }

        this.steps.Add(step);

        return this;
    }

    public MapGenPipelineBuilder add<TStepSettings>(TStepSettings settings)
        where TStepSettings : MapGenStepSettingsBase
    {
        
        var step = (IMapGenStep)Activator.CreateInstance(settings.MapGenStepType);
        step.configure(settings);
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

    private readonly MapGeneratorSettings context;

    public MapGenData data { get { return this.context.data; } }

    public MapGeneratorState Current { get; private set; } = null;
    object IEnumerator.Current => (MapGeneratorState)this.Current;

    public bool isBusy { get { return this.context.hasPendingJobs; } }

    internal MapGenerator(in MapGenPipeline pipeline, in MapGeneratorSettings settings)
    {
        this.pipeline       = pipeline;
        this.context        = settings;

        this.context.initialize();

    }
    public  bool MoveNext()
    {
        // wait for all pending jobs to finish, before continue with next step iteration
        if(this.isBusy) { return true; }

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
                    this.Current.pipe.Current.initialize(this.context);
                    this.Current.step = this.Current.pipe.Current.execute(this.context);

                    this.Current.stepStart = DateTime.Now;
                    this.OnMapGenStepStarted?.Invoke(this.Current.pipe.Current, this.context.data);
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
                        if(this.isBusy) { continue; }

                        this.OnMapGenStepUpdated?.Invoke(this.Current.pipe.Current, this.context.data);

                        if(this.Current.step.MoveNext())
                        {
                            var stepState = this.Current.step.Current;
                        }
                        // map gen step completed
                        else
                        {
                            var duration = DateTime.Now - this.Current.stepStart;
                            Debug.Log($"Map generator step {this.Current.pipe.Current} finished in: {duration}");

                            this.OnMapGenStepFinished?.Invoke(this.Current.pipe.Current, this.context.data);

                            // move on to next step in the pipeline
                            if(this.Current.pipe.MoveNext())
                            {
                                this.Current.pipe.Current.initialize(this.context);
                                this.Current.step = this.Current.pipe.Current.execute(this.context);

                                this.Current.stepStart = DateTime.Now;
                                this.OnMapGenStepStarted?.Invoke(this.Current.pipe.Current, this.context.data);
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

                this.OnMapGenFinished?.Invoke(this.context.data);
                break;
            }
        }

        return false;
    }

    public void Reset()
    {
        this.context.awaitPendingJobs();

        // allow each map gen step to release its previously initialized resources.
        // note: we start releasing the latest steps first as they might rely on resources from previous steps
        foreach(var step in this.pipeline.getSteps.Reverse()) { step.release(this.context); }

        // reset mep generator state
        this.Current        = MapGeneratorState.CreateInitial(this.pipeline);

        this.OnMapGenReseted?.Invoke();
    }

    public void Dispose()
    {
        this.context.awaitPendingJobs();

        // allow each map gen step to release its previously initialized resources.
        // note: we start releasing the latest steps first as they might rely on resources from previous steps
        foreach(var step in this.pipeline.getSteps.Reverse()) { step.release(this.context); }

        this.pipeline.Dispose();
    }
}

public class MapGen_Layout : MonoBehaviour
{
    public Tilemap          floor, obstr;

    public Tile             tile;
    public Tile             wall;
    public Tile             path;

    public MapGeneratorSettings generatorSettings;

    public Step1Settings    step1Settings;
    public Step2Settings    step2Settings;
    public Step2aSettings   step2aSettings;
    public Step3Settings    step3Settings;

    public MapGenerator    executor;

    public bool done = false;

    [Range(0, 1000)]
    public int delayTilemapRefresh = 0;


    // Start is called before the first frame update
    void Start()
    {
        var step3 = new Step3();
        step3.OnProgress += OnStep3Progress;

        var pipeline = MapGenPipeline
                .create()
                    //.add<Step1, Step1Settings>(this.step1Settings)
                    //.add<Step2, Step2Settings>(this.step2Settings)
                    //.add<Step2a, Step2aSettings>(this.step2aSettings)

                    .add(this.step1Settings)
                    .add(this.step2Settings)
                    .add(this.step2aSettings)

                    //.add<Step3, Step3Settings>(this.step3Settings)
                    .add(step3, this.step3Settings)
                .build();

        this.executor = pipeline.createExecutor(this.generatorSettings);

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
        this.floor.ClearAllTiles();
        this.obstr.ClearAllTiles();

        for(int c = 0; c < data.pathSteps.Length + 1; c++)
        {
            var chunkInfo = data.getChunkInfo(c);
            var chunkData = data.getChunkData(c);

            for(int y = 0; y < chunkInfo.bounds.height; y++)
            for(int x = 0; x < chunkInfo.bounds.width; x++)
            {
                var tilePos         = new Vector3Int(chunkInfo.bounds.x + x, -(chunkInfo.bounds.y + y), 0);
                int i               = (y * chunkInfo.bounds.width) + x;
                int floorModuleId   = chunkData[i][MapGenData.Layer.Floor];
                int obstrModuleId   = chunkData[i][MapGenData.Layer.Obstructable];

                if(floorModuleId != -1)
                {
                    this.floor.SetTile(tilePos, this.step3Settings.modules[floorModuleId]);
                }

                if(obstrModuleId != -1)
                {
                    this.obstr.SetTile(tilePos, this.step3Settings.modules[obstrModuleId]);
                }
            }
        }

        System.Threading.Tasks.Task.Delay(this.delayTilemapRefresh).Wait();
    }

    private void OnMapGenStepFinished(in IMapGenStep step, in MapGenData data)
    {
        if(typeof(Step1) == step.GetType())
        {
            floor.ClearAllTiles();

            Vector2Int s = this.step1Settings.pathStart;

            // start tile
            floor.SetTile((Vector3Int)s, this.tile);

            // path
            for(int i = 1; i < data.pathSteps.Length - 1; i++)
            {
                s += new Vector2Int(data.pathSteps[i].x, -data.pathSteps[i].y);
                floor.SetTile((Vector3Int)s, this.tile);
            }

            // end tile
            if(data.pathSteps.Length > 1)
            {
                s += data.pathSteps[data.pathSteps.Length - 1];

                floor.SetTile((Vector3Int)s, this.tile);
            }
        }
        else if(typeof(Step3) == step.GetType())
        {
            this.floor.ClearAllTiles();
            this.obstr.ClearAllTiles();

            for(int c = 0; c < data.pathSteps.Length + 1; c++)
            {
                var chunkInfo = data.getChunkInfo(c);
                var chunkData = data.getChunkData(c);

                for(int y = 0; y < chunkInfo.bounds.height; y++)
                for(int x = 0; x < chunkInfo.bounds.width; x++)
                {
                    var tilePos         = new Vector3Int(chunkInfo.bounds.x + x, -(chunkInfo.bounds.y + y), 0);
                    int i               = (y * chunkInfo.bounds.width) + x;
                    int floorModuleId   = chunkData[i][MapGenData.Layer.Floor];
                    int obstrModuleId   = chunkData[i][MapGenData.Layer.Obstructable];

                    if(floorModuleId != -1)
                    {
                        this.floor.SetTile(tilePos, this.step3Settings.modules[floorModuleId]);
                    }

                    if(obstrModuleId != -1)
                    {
                        this.obstr.SetTile(tilePos, this.step3Settings.modules[obstrModuleId]);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        this.floor?.ClearAllTiles();
        this.executor?.Dispose();
    }

    public void reset()
    {
        this.executor.Reset();
        StartCoroutine(this.executor);
    }
}
