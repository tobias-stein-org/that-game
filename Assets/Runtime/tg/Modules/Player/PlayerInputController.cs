using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    using tg.application;

    public partial class PlayerInputController : SystemBase
    {
        private InputActionMap  playerActions;

        protected override void OnCreate()
        {
            this.RequireForUpdate<Player>();
            this.RequireForUpdate<ApplicationData>();
        }

        protected override void OnStartRunning()
        {
            this.playerActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.Result.FindActionMap("Player");
        }

        protected override void OnUpdate()
        {
            var speed   = 10.0f * this.World.Time.DeltaTime;
            var move    = playerActions.FindAction("move").ReadValue<Vector2>();

            foreach(var localTransform in SystemAPI.Query<RefRW<LocalTransform>>().WithAll<Player>())
            {
                localTransform.ValueRW.Position += new float3(move.x, move.y, 0.0f) * speed;
            }
        }
    }
}
