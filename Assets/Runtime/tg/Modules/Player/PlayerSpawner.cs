using Unity.Entities;
using UnityEngine.Scripting;

namespace tg.player
{
    using tg.level;

    /// <summary>
    /// Simple player spawn system. 
    /// </summary>
    public partial struct PlayerSpawner : ISystem
    {
        void OnCreate (ref SystemState state)
		{
			state.RequireForUpdate<PlayerData>();
            state.RequireForUpdate<LevelData>();
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
                    var chunk0              = SystemAPI.GetSingleton<LevelData>().getChunk(0);

                    var spawnLocation       = new Unity.Mathematics.float3(
                        chunk0.bounds.x     + (chunk0.bounds.width  / 2),
                        chunk0.bounds.y     + (chunk0.bounds.height / 2),
                        -1.0f);

                    // instantiate a new player entity
                    var entity              = tg.spawn.SpawnRequest.create(playerData.prefab, spawnLocation, out EntityCommandBuffer ECB);
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

