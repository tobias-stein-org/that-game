using UnityEngine;

using Unity.Entities;

namespace tg.player
{
    using tg.application;

    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct PlayerController : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireAnyForUpdate(StateManager.state(this));
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
