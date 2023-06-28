using UnityEngine;
using UnityEngine.InputSystem;

using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

namespace tg.player
{
    using tg.ability;
    using tg.ability.entities;

    using tg.ability.events;
    using tg.application;
    using tg.events;

    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial class PlayerInput : SystemBase
    {
        private InputActionMap  playerActions;

        protected override void OnCreate()
        {
            this.RequireForUpdate<Player>();
            this.RequireForUpdate(StateManager.state(this));
        }

        protected override void OnStartRunning()
        {
            this.playerActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Player");
        }

        protected override void OnUpdate()
        {
            foreach(var (input, player, transform) in SystemAPI.Query<RefRW<PlayerInputData>, Player, SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>())
            //foreach(var playerInput in SystemAPI.Query<RefRW<PlayerInputData>>().WithAll<Player>())
            {
                float2 position         = (Vector2)transform.Value.position;
                input.ValueRW.move      = playerActions.FindAction("move").ReadValue<Vector2>();
                input.ValueRW.melee     = playerActions.FindAction("melee").ReadValue<float>();
                input.ValueRW.cast      = playerActions.FindAction("cast").ReadValue<float>();

                if(playerActions.FindAction("ability0").WasPerformedThisFrame())
                {
				    EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                    {
                        entity      = player.entity,
                        ability     = "FIREBALL_1",
                        point       = position + (input.ValueRO.look * 1.5f),
                        direction   = input.ValueRO.look
                    });
                }

                if(playerActions.FindAction("ability1").WasPerformedThisFrame())
                {
				    EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                    {
                        entity      = player.entity,
                        ability     = "ICEBLAST_1",
                        point       = position + (input.ValueRO.look * 1.5f),
                        direction   = input.ValueRO.look
                    });
                }

                if(playerActions.FindAction("melee").WasPerformedThisFrame())
                {
                    EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                    {
                        entity      = player.entity,
                        ability     = "MELEE_ATT",
                        point       = position + (input.ValueRO.look * 1.5f),
                        direction   = input.ValueRO.look
                    });
                }
            }
        }
    }

    public struct PlayerInputData : IComponentData
    {
        public float2   look;
        public float2   move;

        /// <summary>
        /// Degrees per second.
        /// </summary>
        public float    turnSpeed;

        /// <summary>
        /// Units per second.
        /// </summary>
        public float    moveSpeed;

        public float    melee;
        public float    cast;
    }
}
