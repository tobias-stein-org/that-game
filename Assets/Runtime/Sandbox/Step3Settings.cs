using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Step3")]
public class Step3Settings : MapGenStepSettings
{
    public int              numAttemptsToSolveMapChunk = 3;

    [Range(0.0f, 100.0f)]
    public float            similarityThreshold = 2.0f;

    [Range(0.0f, 100.0f)]
    public float            similarityPercentile = 95.0f;

    [Range(1e-5f, 1.0f)]
    public float            mapChunkObstructivness = 0.05f;

    public void OnValidate()
    {
        this.numAttemptsToSolveMapChunk = Mathf.Max(1, this.numAttemptsToSolveMapChunk);
        this.mapChunkObstructivness = Mathf.Clamp(this.mapChunkObstructivness, 1e-5f, 1.0f);
    }
}
