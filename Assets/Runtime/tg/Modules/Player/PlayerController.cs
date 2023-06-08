using UnityEngine;

using Unity.Entities;

namespace tg.player
{
    [UpdateInGroup(typeof(LateSimulationSystemGroup))]
    public partial struct PlayerController : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInputData>();
        }

        public void OnDestory(ref SystemState state)
        {
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach(var (rigidBody, playerInput) in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>, PlayerInputData>().WithAll<Player>())
            {
                // set velocity from player input to drive player object, physics will take care of collicion handling for us
                rigidBody.Value.velocity = playerInput.moveXY * playerInput.moveSpeed;
            }
        }
    }
}
