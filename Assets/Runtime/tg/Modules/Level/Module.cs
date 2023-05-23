using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace tg.level
{
    [CreateAssetMenu(menuName = "tg/Level/Create Module")]
    public class Module : UnityEngine.Tilemaps.Tile
    {
        public LevelData.Tile.ConstructionType  constructionType    = LevelData.Tile.ConstructionType.Walkable;

        public float                            weight              = 1.0f;

        public void OnValidate()
        {
            this.weight = Mathf.Max(0.0f, this.weight);
        }
    }
}
