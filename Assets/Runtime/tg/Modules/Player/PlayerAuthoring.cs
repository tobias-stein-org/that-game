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
                var playerEntity        = GetEntity(TransformUsageFlags.Dynamic);
                
                //AddComponent<URPMaterialPropertyBaseColor>(playerEntity);
                //SetComponent<URPMaterialPropertyBaseColor>(playerEntity, new URPMaterialPropertyBaseColor { Value = new float4(1.0f, 0.0f, 0.0f, 1.0f) });
            }
        }
    }
}
