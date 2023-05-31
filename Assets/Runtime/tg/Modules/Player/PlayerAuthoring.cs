using UnityEngine;
using Unity.Entities;
using Unity.Rendering;
using Unity.Mathematics;

namespace tg.player
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public class Baker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
            }
        }
    }
}

