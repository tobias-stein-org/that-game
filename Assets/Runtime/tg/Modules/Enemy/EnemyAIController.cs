using System.Linq;

using UnityEngine;

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.enemy
{
    using tg.application;
    using tg.ability.events;
    using tg.events;
    using tg.ai.steering;

    namespace entities
    {
        using tg.application.entities;
        using tg.ai.entities;
        using tg.ai.steering.entities;
        using tg.ai.steering.behaviour.entities;

        readonly partial struct AIEnemy : IAspect
        {
            readonly DynamicBuffer<BehaviourContextData>        behaviourContext;
            readonly RefRW<EnemyInputData>                      inputData;

            readonly public  Entity                             entity;

            public BehaviourSolver.BehaviourContextDataInternal solvedContext
            {
                get { return this.behaviourContext.Reinterpret<BehaviourSolver.BehaviourContextDataInternal>()[IBehaviourContext<Solved>.ID]; }
            }

            public EnemyInputData                               input
            {
                get { return this.inputData.ValueRO; }
                set { this.inputData.ValueRW = value; }
            }
        }


        [BurstCompile]
        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        [UpdateBefore(typeof(EnemyMove))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct EnemyTurn : ISystem
        {
            private Unity.Mathematics.Random  random;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(this));
                state.RequireForUpdate<EnemyInputData>();

                this.random = Unity.Mathematics.Random.CreateFromIndex((uint)state.SystemHandle.GetHashCode());
            }

            [BurstCompile]
            public void OnUpdate(ref SystemState state)
            {
                foreach(var (input, behaviourBuffer) in SystemAPI.Query<RefRW<EnemyInputData>, DynamicBuffer<BehaviourContextData>>().WithAll<Enemy>())
                {
                    var behaviour           = behaviourBuffer.Reinterpret<BehaviourSolver.BehaviourContextDataInternal>()[IBehaviourContext<Solved>.ID];

                    if(!behaviour.context.isValid) { continue; }

                    var context             = behaviour.context.normalize();
                    var csum                = context.csum();

                    if(csum > 1e-5f)
                    {
                        var rng             = this.random.NextFloat(csum);
                        var i               = 0;
                        var acc             = 0.0f;
                        for(i               = 0; i < context.length - 1; i++)
                        {
                            if(acc          >= rng) { break; }
                            acc             += context[i];
                        }

                        //note: code is basically the conversion of UnityEngine.Quaternion.RotateTowards
                        var q0 = quaternion.LookRotation(new float3(input.ValueRO.look, 0f), Vector3.forward);
                        var q1 = quaternion.LookRotation(new float3(BehaviourContext.segmentDir[i], 0f), Vector3.forward);

                        float num = math.min(math.abs(math.dot(q0, q1)), 1f);
                        num = num > 0.999999f ? 0f : (math.acos(num) * 2f * 57.29578f);

                        var look = num == 0f ? q1 : math.slerp(q0, q1, math.min(1f, SystemAPI.Time.DeltaTime * input.ValueRO.turnSpeed / num));
                        input.ValueRW.move  = BehaviourContext.segmentDir[i];
                        input.ValueRW.look  = math.mul(look, new float3(0f, 0f, 1f)).xy;
                    }
                }
            }
        }

        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial struct EnemyMove : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(this));
                state.RequireForUpdate<EnemyInputData>();
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach(var (rigidBody, input) in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>, EnemyInputData>().WithAll<Enemy>())
                {
                    if(math.lengthsq(input.move) > 1e-5f)
                    {
                        //rigidBody.Value.velocity = input.look * input.moveSpeed;
                        rigidBody.Value.AddForce(input.look * input.moveSpeed);
                        rigidBody.Value.velocity = Vector2.ClampMagnitude(rigidBody.Value.velocity, input.moveSpeed);
                    }
                    //else
                    //{
                    //    rigidBody.Value.velocity = Vector2.zero;
                    //}
                }
            }
        }
    }
}
