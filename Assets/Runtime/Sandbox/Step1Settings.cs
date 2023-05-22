using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "MapGen/Step1")]
public class Step1Settings : MapGenStepSettings<Step1>
{
    public Vector2Int   pathStart;
    public int          pathLength;

    [Range(0, 1)]
    public float        pathStraightness;
    [Range(0, 1)]       
    public float        pathCurvature;
    [Range(0, 1)]       
    public float        pathDensity;
    [Range(0, 1)]       
    public float        pathRandomness;

    public bool         pathIntersection;
  
    public bool         allowBacktracking;
    public int          maxBacktracks;

    public void OnEnable()
    {
        float sum = this.pathStraightness + this.pathCurvature + this.pathDensity;

        this.pathStraightness    /= sum;
        this.pathCurvature       /= sum;
        this.pathDensity         /= sum;
    }
}