using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Step2")]
public class Step2Settings : MapGenStepSettings<Step2>
{
    public Vector2Int   mapChunkDimensions          = new Vector2Int(20, 20);

    public int          mapChunkWallSize            = 1;

    public int          walkablePathThickness       = 1;

    [Range(0, 1)]
    public float        walkablePathDisplacement    = 0.0f;


    public void OnValidate()
    {
        this.mapChunkDimensions.x   = Mathf.Max(5, this.mapChunkDimensions.x);
        this.mapChunkDimensions.y   = Mathf.Max(5, this.mapChunkDimensions.y);

        var minSideSize             = Mathf.Min(this.mapChunkDimensions.x, this.mapChunkDimensions.y);
        var maxWallSize             = Mathf.FloorToInt((float)minSideSize / 2.0f);
        this.mapChunkWallSize       = Mathf.Clamp(this.mapChunkWallSize, 0, maxWallSize);

        this.walkablePathThickness  = Mathf.Clamp(walkablePathThickness, 0, minSideSize - (2 * this.mapChunkWallSize));
    }
}
