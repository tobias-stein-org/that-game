using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

[CreateAssetMenu(menuName = "MapGen/Create Module")]
public class Module : Tile
{
    public float   weight  = 1;

    public void OnValidate()
    {
        this.weight = Mathf.Max(0.0f, this.weight);
    }
}