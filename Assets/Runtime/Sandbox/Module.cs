using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public enum TileConstructionType
{
    Undefined       = 0,
    Obstructed      = 1 << 0,
    Walkable        = 1 << 1,
    Empty           = 1 << 2
}


[CreateAssetMenu(menuName = "MapGen/Create Module")]
public class Module : Tile
{
    public float                weight              = 1.0f;

    public TileConstructionType constructionType    = TileConstructionType.Undefined;

    public void OnValidate()
    {
        this.weight = Mathf.Max(0.0f, this.weight);
    }
}