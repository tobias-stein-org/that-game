using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    using tg.application;

    public partial class PlayerInput : SystemBase
    {
        private InputActionMap  playerActions;

        protected override void OnCreate()
        {
            this.RequireForUpdate<Player>();
            this.RequireForUpdate<ApplicationDataLoaded>();
        }

        protected override void OnStartRunning()
        {
            this.playerActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Player");
        }

        protected override void OnUpdate()
        {
            foreach(var playerInput in SystemAPI.Query<RefRW<PlayerInputData>>().WithAll<Player>())
            {
                playerInput.ValueRW.moveXY = playerActions.FindAction("move").ReadValue<Vector2>();
            }
        }
    }

    public struct PlayerInputData : IComponentData
    {
        public Vector2  moveXY;

        public float    moveSpeed;
    }
}
