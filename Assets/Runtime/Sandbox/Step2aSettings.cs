using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MapGen/Step2a")]
public class Step2aSettings : MapGenStepSettings
{
    [Range(0.0f, 1.0f)]
    public float pathConvolution = 0.8f;
}
