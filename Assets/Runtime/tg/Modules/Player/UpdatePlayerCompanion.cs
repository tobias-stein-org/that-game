using Unity.Burst;
using Unity.Transforms;
using Unity.Entities;
using UnityEngine.Scripting;
using Unity.Collections.LowLevel.Unsafe;

namespace tg.player
{
	/// <summary>
	/// Dedicated system to keep player entity and it's Unity scene companion GameObject in sync.
	/// </summary>
	[UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct UpdatePlayerCompanion : ISystem
    {
		void OnCreate(ref SystemState state)
		{
			state.RequireForUpdate<Player>();
		}

		void OnDestroy(ref SystemState state)
		{
		}

		void OnUpdate(ref SystemState state)
		{
			foreach(var (player, localTransform) in SystemAPI.Query<Player, LocalTransform>())
			{
				var playerData = SystemAPI.ManagedAPI.GetComponent<PlayerData>(player.playerData);

				playerData.companion.transform.position = localTransform.Position;
				playerData.companion.transform.rotation = localTransform.Rotation;
			}
		}
    }
}

