using Unity.Entities;
using UnityEngine.Scripting;

namespace tg.player
{
    /// <summary>
    /// Simple player spawn system. 
    /// </summary>
    public partial struct PlayerSpawner : ISystem
    {
        void OnCreate (ref SystemState state)
		{
			state.RequireForUpdate<PlayerData>();
		}

		void OnDestroy (ref SystemState state)
		{
		}

		void OnUpdate (ref SystemState state)
		{
            foreach(var (playerData, playerDataEntity) in SystemAPI.Query<PlayerData>().WithEntityAccess())
            {
                if(!playerData.isAlive)
                {
                    // instantiate a new player entity
                    var entity              = tg.spawn.SpawnRequest.create(playerData.prefab, new Unity.Mathematics.float3(24.0f, 13.5f, -1.0f), out EntityCommandBuffer ECB);
                    {
                        ECB.AddComponent<Player>(entity, new Player
                        {
                            playerData      = playerDataEntity
                        });
                    }

                    // set isAlive flag true, when player entity is spawned.
                    playerData.isAlive      = true;
                    ECB.SetComponent<PlayerData>(playerDataEntity, playerData);
                }
            }

		}
    }
}

