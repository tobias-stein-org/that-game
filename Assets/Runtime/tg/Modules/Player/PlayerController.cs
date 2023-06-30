using UnityEngine;

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.player
{
    using tg.ai;
    using tg.application.entities;

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
                    if(math.lengthsq(input.ValueRO.move) > 1e-5f)
                    {
                        //note: code is basically the conversion of UnityEngine.Quaternion.RotateTowards
                        var q0 = quaternion.LookRotation(new float3(input.ValueRO.look, 0f), Vector3.forward);
                        var q1 = quaternion.LookRotation(new float3(input.ValueRO.move, 0f), Vector3.forward);

                        float num = math.min(math.abs(math.dot(q0, q1)), 1f);
                        num = num > 0.999999f ? 0f : (math.acos(num) * 2f * 57.29578f);

                        var look = num == 0f ? q1 : math.slerp(q0, q1, math.min(1f, SystemAPI.Time.DeltaTime * input.ValueRO.turnSpeed / num));
                        input.ValueRW.look  = math.mul(look, new float3(0f, 0f, 1f)).xy;
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
                foreach(var (rigidBody, input) in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>, PlayerInputData>().WithAll<Player>())
                {
                    if(math.lengthsq(input.move) > 1e-5)
                    {
                        // set velocity from player input to drive player object, physics will take care of collicion handling for us
                        rigidBody.Value.velocity = input.move * input.moveSpeed;
                    }
                    else
                    {
                        rigidBody.Value.velocity = Vector2.zero;
                    }
                }
            }
        }
    }
}
