using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace tg.camera
{
    using tg.player;

    public partial class CameraSystem : SystemBase
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate<PlayerData>();
        }

        protected override void OnUpdate()
        {
            var playerDataEntity = SystemAPI.ManagedAPI.GetSingletonEntity<PlayerData>();
            var playerData = SystemAPI.ManagedAPI.GetComponent<PlayerData>(playerDataEntity);

            var entity = tg.spawn.SpawnRequest.create(playerData.prefab, new Unity.Mathematics.float3(24.0f, 13.5f, 0.0f), out EntityCommandBuffer ECB, 1);
            {
                ECB.AddComponent<Player>(entity, new Player
                {
                    playerData = playerDataEntity
                });
            }

            this.Enabled = false;
        }
    }
}

