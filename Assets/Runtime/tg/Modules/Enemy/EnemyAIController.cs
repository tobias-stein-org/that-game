using System.Linq;

using UnityEngine;

using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.enemy
{
    using tg.application;
    using tg.ai;
    using tg.ability.events;
    using tg.events;

    namespace entities
    {
        using tg.application.entities;
        using tg.ai.entities;
        using tg.ai.behaviour.entities;

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


        [UpdateInGroup(typeof(LateSimulationSystemGroup))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        [RequireMatchingQueriesForUpdate]
        public partial struct EnemyAIController : ISystem
        {
            private Unity.Mathematics.Random  random;

            public void OnCreate(ref SystemState state)
            {
                state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<EnemyAIController>(state.SystemHandle)));

                this.random = Unity.Mathematics.Random.CreateFromIndex((uint)state.SystemHandle.GetHashCode());
            }

            public void OnUpdate(ref SystemState state)
            {
                foreach(var (aiEnemy, sensor, rb) in SystemAPI.Query<AIEnemy, Sensor, SystemAPI.ManagedAPI.UnityEngineComponent<Rigidbody2D>>())
                {
                    // TODO: at some point this logic has to be moved to a behviour tree
                    {
                        var player  = sensor.query().withTag(tags.Player).FirstOrDefault();

                        // if enemy can see a player AND has the Pursue behaviour ...
                        if(player.isValid && state.EntityManager.HasComponent<Pursue>(aiEnemy.entity))
                        {
                            var pos             = (float2)rb.Value.position;
                            var dir             = math.normalize(player.point - (float2)rb.Value.position);

                            if(player.distance > 1.5f)
                            {
                                if(UnityEngine.Random.value < 0.2f)
                                {
                                    EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                                    {
                                        entity      = aiEnemy.entity,
                                        ability     = "FIREBALL_1",
                                        point       = pos + (dir * 1.5f),
                                        direction   = dir
                                    });
                                }
                            }
                            // we are in attack range ...
                            else
                            {
                                if(UnityEngine.Random.value < 0.01f)
                                {
                                    EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                                    {
                                        entity      = aiEnemy.entity,
                                        ability     = "MELEE_ATT",
                                        point       = pos + (dir * 1.5f),
                                        direction   = dir
                                    });
                                }
                            }


                            // start pursue the current percied player location
                            var pursue          = state.EntityManager.GetComponentData<Pursue>(aiEnemy.entity);
                            {
                                pursue.target   = player.point;
                            }
                            state.EntityManager.SetComponentData<Pursue>(aiEnemy.entity,pursue);
                            state.EntityManager.SetComponentEnabled<Pursue>(aiEnemy.entity, true);

                            // stop any wandering
                            state.EntityManager.SetComponentEnabled<Wander>(aiEnemy.entity, false);
                        }
                        // if we can't see a player ...
                        else
                        {
                            // AND we are not Pursueing any one ...
                            if(!state.EntityManager.IsComponentEnabled<Pursue>(aiEnemy.entity))
                            {
                                // go back to wandering
                                state.EntityManager.SetComponentEnabled<Wander>(aiEnemy.entity, true);
                            }
                        }
                    }
                }
            }
        }
    }
}
