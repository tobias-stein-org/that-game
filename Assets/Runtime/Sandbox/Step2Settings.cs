using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Step2")]
public class Step2Settings : MapGenStepSettings
{
    public Vector2Int   mapChunkDimensions         = new Vector2Int(20, 20);

    public int          mapChunkWallSize            = 1;

    public int          walkablePathThickness       = 1;

    [Range(0, 1)]
    public float        walkablePathDisplacement    = 0.0f;
}
