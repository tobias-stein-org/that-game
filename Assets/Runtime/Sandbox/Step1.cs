
using UnityEngine;
using Unity.Collections;

using PathStep          = UnityEngine.Vector2Int;
using Path              = Unity.Collections.NativeList<UnityEngine.Vector2Int>;
using PathStepStack     = Unity.Collections.NativeList<Unity.Collections.NativeList<Step1.PathStepChoice>>;


public partial struct MapGenData
{
    public NativeArray<Vector2Int> pathSteps;
}

public class Step1 : MapGenStep<Step1Settings>
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



    private Path            path;

    private PathStepStack   stack;
    private int             backtracks;    


    private int             currentStep         { get { return this.path.Length; } }

    private System.Random   random;

    public override void initialize(ref MapGenContext context)
    {
        context.data.pathSteps = new NativeArray<PathStep>(this.settings.pathLength - 1, Allocator.Persistent);
    }

    public override void release(ref MapGenContext context)
    {
        if(context.data.pathSteps.IsCreated) { context.data.pathSteps.Dispose(); }
    }

    public override void execute(ref MapGenContext context)
    {
        this.reset();

        // check, if done.
        while(this.currentStep < this.settings.pathLength)
        {
            // generate new possible next steps, if needed
            if(this.stack.Length < this.currentStep) { this.stack.Add(this.computePossibleSteps()); }

            ref NativeList<PathStepChoice> nextSteps = ref this.stack.ElementAt(this.stack.Length - 1);

            // there are no possible next steps? Try backtracking
            if(nextSteps.IsEmpty)
            {
                if(!this.settings.allowBacktracking || this.backtracks > this.settings.maxBacktracks)
                {
                    this.reset();
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
            }
        }

        for(int i = 0; i < this.path.Length - 1; i++) { context.data.pathSteps[i] = this.path[i + 1] - this.path[i]; }
    }

    public override void Dispose()
    {
        this.cleanup();
    }

    public void reset()
    {
        this.cleanup();

        this.path           = new Path(Allocator.Persistent);
        this.stack          = new PathStepStack(this.settings.pathLength, Allocator.Persistent);

        this.backtracks     = 0;
        this.random         = new System.Random();

        this.path.Add(this.settings.pathStart);

    }

    private void cleanup()
    {
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

        
        // do a random move, if all scores were zero or specified path randomness value wins
        if(this.settings.pathRandomness > this.random.NextDouble())
        {
            choice = this.random.Next(0, nextSteps.Length);
        }
        else
        {
            float sum = 0.0f;
            for(int i = 0; i < nextSteps.Length; i++) { sum += nextSteps[i].score; }

            float rnd = sum * (float)this.random.NextDouble();
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
            if(!this.settings.pathIntersection && this.path.Contains(nextStep)) { continue; }

            steps.Add(new PathStepChoice { direction = direction, score = this.computeNextStepScore(ref nextStep) });
        }

        // sort by score, highest first
        steps.Sort();
        for(int i = 0; i < steps.Length; i++) { steps.ElementAt(i).score -= steps[steps.Length - 1].score; }

        return steps;
    }

    private float computeNextStepScore([ReadOnly] ref PathStep nextStep)
    {
        float distance      = this.euclideanDistance(ref this.path.ElementAt(0), ref nextStep);
        float curvature     = this.manhattenCurvature(ref this.path, ref nextStep);
        float area          = this.area(ref this.path, ref nextStep);

        // values between 0.0 and 1.0
        float density       = (float)(this.path.Length + 1) / area;
        float straightness  = distance / (float)(this.path.Length);
        float curvature01   = this.path.Length < 2 ? 0.0f : (curvature / (float)(this.path.Length - 1));

        return (straightness - this.settings.pathStraightness) * (curvature01 - this.settings.pathCurvature) * (density - this.settings.pathDensity);
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
