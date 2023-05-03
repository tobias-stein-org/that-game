using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;

using UnityEditor.UIElements;

using PathLength        = System.Int32;
using PathStep          = UnityEngine.Vector2Int;
using Path              = Unity.Collections.NativeList<UnityEngine.Vector2Int>;
using PathStepStack     = Unity.Collections.NativeList<Unity.Collections.NativeList<MapGen_Layout.PathStepChoice>>;
using UnityEngine.Tilemaps;

public class MapGen_Layout : MonoBehaviour
{

    private static readonly Vector2Int[] Direction = new Vector2Int[]
    {
        Vector2Int.up,    
        Vector2Int.down,    
        Vector2Int.left,    
        Vector2Int.right,    
    };

    public struct PathStepChoice : System.IComparable<PathStepChoice>
    {
        public float        score;
        public PathStep     direction;

        /// <summary>
        /// Sort by score in descending order, that is, highest score first.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(PathStepChoice other) { return other.score.CompareTo(this.score); }
    }

    public Vector2Int       pathStart           = Vector2Int.zero;

    public PathLength       pathLength          = 7;
    //public int              pathBranches        = 0;

    [Range(0,1)]
    public float            pathStraightness    = 1.0f;
    [Range(0,1)]
    public float            pathCurvature       = 0.0f;
    [Range(0,1)]
    public float            pathDensity         = 0.0f;
    [Range(0,1)]
    public float            pathRandomness      = 0.0f;

    public bool             pathIntersection    = false;

    private Path            path;

    private PathStepStack   stack;
    private int             backtracks;      

    public bool             allowBacktracking   = true;
    public int              maxBacktracks       = 100;
    public bool             done                = false;


    private int             currentStep         { get { return this.path.Length; } }


    public Tilemap          tilemap;
    public TileBase         tile;

    private float           lastStepUpdate;
    [Range(0,1)]
    public float            delay = 0.1f;

    // Start is called before the first frame update
    void Start()
    {
        this.init();   
    }

    private void OnDestroy()
    {
        this.cleanup();
    }

    public void getPath(out NativeArray<Vector2Int> pathSteps)
    {
        pathSteps = new NativeArray<PathStep>(this.path.Length - 1, Allocator.Persistent);
        for(int i = 0; i < this.path.Length - 1; i++)
        {
            pathSteps[i] = this.path[i + 1] - this.path[i];
        }
    }

    // Update is called once per frame
    void Update()
    {
        
        if(!this.done && (Time.time - this.lastStepUpdate > this.delay))
        {
            this.done = this.step();

            // repaint tilemap
            if(this.tilemap != null)
            {
                this.tilemap.ClearAllTiles();

                // start tile
                this.tilemap.SetTile((Vector3Int)this.path[0], this.tile);
                this.tilemap.SetColor((Vector3Int)this.path[0], Color.blue);

                // path
                for(int i = 1; i < this.path.Length - 1; i++)
                {
                    this.tilemap.SetTile((Vector3Int)this.path[i], this.tile);
                    this.tilemap.SetColor((Vector3Int)this.path[i], Color.white);
                }

                // end tile
                if(this.path.Length > 1)
                {
                    this.tilemap.SetTile((Vector3Int)this.path[this.path.Length - 1], this.tile);
                    this.tilemap.SetColor((Vector3Int)this.path[this.path.Length - 1], Color.red);
                }
            }

            this.lastStepUpdate = Time.time;
        }   
    }

    public void init()
    {
        this.cleanup();

        this.path           = new Path(Allocator.Persistent);
        this.stack          = new PathStepStack(this.pathLength, Allocator.Persistent);

        this.done           = false;
        this.backtracks     = 0;

        this.path.Add(this.pathStart);

        this.lastStepUpdate = Time.time;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns>true if done, that is a valid path of desired length has been formed.</returns>
    private bool step()
    {
        // check, if done.
        if(this.currentStep >= this.pathLength) { return true; }

        // generate new possible next steps, if needed
        if(this.stack.Length < this.currentStep) { this.stack.Add(this.computePossibleSteps()); }


        ref NativeList<PathStepChoice> nextSteps = ref this.stack.ElementAt(this.stack.Length - 1);

        // there are no possible next steps? Try backtracking
        if(nextSteps.IsEmpty)
        {
            if(!this.allowBacktracking || this.backtracks > this.maxBacktracks)
            {
                this.init();
            }
            else
            {
                // remove previously added path step and stack element
                this.stack.RemoveAt(this.stack.Length - 1);
                // also remove previous path element, since it caused a deadend
                this.path.RemoveAt(this.path.Length - 1);

                this.backtracks++;

                // sanity check (should never happen)
                if(this.path.Length == 0) { throw new System.Exception("Insufficent path steps for backtracking!"); }
            }
        }
        else
        {
            // select a move by chance
            var nextStep = this.path.ElementAt(this.path.Length - 1) + this.takeNextStep(ref nextSteps);

            // add next path step
            this.path.Add(nextStep);

            // update path info
            // TODO: !
        }


        return false;
    }

    private void cleanup()
    {
        this.tilemap?.ClearAllTiles();

        if(this.stack.IsCreated) { this.stack.Dispose(); }
        if(this.path.IsCreated) { this.path.Dispose(); }
    }

    /// <summary>
    /// Note: this method will also remove the taken step from the input list.
    /// </summary>
    /// <param name="nextSteps"></param>
    /// <returns></returns>
    private PathStep takeNextStep(ref NativeList<PathStepChoice> nextSteps)
    {
        int choice = 0;
        float t = (float)(this.currentStep + 1) / (float)this.pathLength;

        // do a random move, if all scores were zero or specified path randomness value wins
        if(this.pathRandomness > Random.value)
        {
            choice = Random.Range(0, nextSteps.Length);
        }
        else
        {
            float sum = 0.0f;
            for(int i = 0; i < nextSteps.Length; i++) { sum += nextSteps[i].score; }

            float rnd = Random.Range(0.0f, sum);
            float cumsum = 0.0f;
            for(choice = 0; choice < nextSteps.Length - 1; choice++) { cumsum += nextSteps[choice].score; if(cumsum >= rnd) { break; } }
            
        }
        PathStep nextStep = nextSteps[choice].direction;
        nextSteps.RemoveAt(choice);

        return nextStep;
    }

    private NativeList<PathStepChoice> computePossibleSteps()
    {
        NativeList<PathStepChoice> steps = new NativeList<PathStepChoice>(4, Allocator.Persistent);

        ref PathStep  thisPath = ref this.path.ElementAt(this.path.Length - 1);
        PathStep? lastPath = this.path.Length > 1 ? this.path.ElementAt(this.path.Length - 2) : null;

        // update score weighting
        foreach(var direction in Direction)
        {
            var nextStep = thisPath + direction;

            // do not step-back
            if(lastPath.HasValue && nextStep == lastPath.Value) { continue; }

            // prevent path intersections, if disabled
            if(!this.pathIntersection && this.path.Contains(nextStep)) { continue; }

            steps.Add(new PathStepChoice { direction = direction, score = this.computeNextStepScore(ref nextStep) });
        }

        // sort by score, highest first
        steps.Sort();
        for(int i = 0; i < steps.Length; i++) { steps.ElementAt(i).score -= steps[steps.Length - 1].score; }

        return steps;
    }

    private float constraintEasing(float contraint) { return Mathf.Pow(contraint, 150); }

    private float computeNextStepScore([ReadOnly] ref PathStep nextStep)
    {
        float distance      = this.euclideanDistance(ref this.path.ElementAt(0), ref nextStep);
        float curvature     = this.manhattenCurvature(ref this.path, ref nextStep);
        float area          = this.area(ref this.path, ref nextStep);

        // values between 0.0 and 1.0
        float density       = (float)(this.path.Length + 1) / area;
        float straightness  = distance / (float)(this.path.Length);
        float curvature01   = this.path.Length < 2 ? 0.0f : (curvature / (float)(this.path.Length - 1));

        float sum           = this.pathStraightness + this.pathCurvature + this.pathDensity;
        return (straightness - this.pathStraightness / sum) * (curvature01 - this.pathCurvature / sum) * (density - this.pathDensity / sum);
    }

    private float euclideanDistance([ReadOnly] ref PathStep start, [ReadOnly] ref PathStep end)
    {
        return (end - start).magnitude;
    }

    private float manhattenCurvature([ReadOnly] ref Path path, [ReadOnly] ref PathStep next)
    {
        int curvature = 0;

        for (int i = 0; i < path.Length - 2; i++)
        {
            ref Vector2Int stepA = ref path.ElementAt(i);
            ref Vector2Int stepB = ref path.ElementAt(i + 1);
            ref Vector2Int stepC = ref path.ElementAt(i + 2);

            Vector2Int dirAB = stepB - stepA;
            Vector2Int dirBC = stepC - stepB;

            curvature += 1 - ((dirBC.x * dirAB.x) + (dirBC.y * dirAB.y));
        }

        // add curvature for the next step
        if(path.Length > 1)
        {
            ref Vector2Int stepX = ref path.ElementAt(path.Length - 2);
            ref Vector2Int stepY = ref path.ElementAt(path.Length - 1);

            Vector2Int dirXY = stepY - stepX;
            Vector2Int dirYZ = next - stepY;

            curvature += 1 - ((dirYZ.x * dirXY.x) + (dirYZ.y * dirXY.y));
        }

        return curvature;
    }

    private float area([ReadOnly] ref Path path, [ReadOnly] ref PathStep next)
    {
        int xMin = next.x, xMax = next.x;
        int yMin = next.y, yMax = next.y;

        for(int i = 0; i < path.Length; i++)
        {
            ref var step = ref path.ElementAt(i);

            xMin = Mathf.Min(xMin, step.x);
            yMin = Mathf.Min(yMin, step.y);
            xMax = Mathf.Max(xMax, step.x);
            yMax = Mathf.Max(yMax, step.y);
        }

        var width  = xMax - xMin + 1;
        var height = yMax - yMin + 1;


        return width * height;
    }
}
