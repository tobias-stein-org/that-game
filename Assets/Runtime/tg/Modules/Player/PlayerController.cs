using UnityEngine;

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.player
{
    using tg.application;

    namespace entities
    {

        [BurstCompile]
        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        [UpdateBefore(typeof(PlayerMove))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct PlayerTurn : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(this));
                state.RequireForUpdate<PlayerInputData>();
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                foreach(var input in SystemAPI.Query<RefRW<PlayerInputData>>().WithAll<Player>())
                {
                    if(math.lengthsq(input.ValueRO.move) > Physics2D.velocityThreshold)
                    {
                        var r0 = Quaternion.LookRotation((Vector2)input.ValueRO.look, Vector3.forward);
                        var r1 = Quaternion.LookRotation((Vector2)input.ValueRO.move, Vector3.forward);

                        input.ValueRW.look = (Vector2)(Quaternion.RotateTowards(r0, r1, SystemAPI.Time.DeltaTime * input.ValueRO.turnSpeed) * Vector3.forward);
                    }
                }
            }
        }

        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct PlayerMove : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(this));
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
                    rigidBody.Value.velocity = playerInput.move * playerInput.moveSpeed;
                }
            }
        }
    }
}
