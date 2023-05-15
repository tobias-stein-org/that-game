using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Step3")]
public class Step3Settings : MapGenStepSettings
{
    public int              numAttemptsToSolveMapChunk = 3;

    public float            similarityThreshold = 5.0f;

    public void OnValidate()
    {
        this.numAttemptsToSolveMapChunk = Mathf.Max(1, this.numAttemptsToSolveMapChunk);
    }
}
