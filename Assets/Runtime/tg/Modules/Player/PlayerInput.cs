using UnityEngine;
using UnityEngine.InputSystem;

using Unity.Entities;
using Unity.Mathematics;

namespace tg.player
{
    using tg.application.entities;


    using tg.ability.events;
    using tg.events;
    using tg.ui.menu.events;

    namespace entities
    {
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
                this.playerActions = SystemAPI.ManagedAPI.GetSingleton<ApplicationData>().inputActions.FindActionMap("Player");

                this.playerActions.FindAction("pause").performed += this.onPause;

            }

            protected override void OnStopRunning()
            {
                this.playerActions.FindAction("pause").performed -= this.onPause;
            }

            private void onPause(InputAction.CallbackContext ctx)
            {
                if(!ctx.action.IsPressed())
                {
                    EventQueue.publish(new OpenMenuEvent { name = ui.menu.menus.PAUSE_MENU, push = false });
                }
            }

            protected override void OnUpdate()
            {
                foreach(var (input, player, transform, entity) in SystemAPI.Query<RefRW<PlayerInputData>, Player, SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>().WithEntityAccess())
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
                            entity      = entity,
                            ability     = "FIREBALL_1",
                            point       = position + (input.ValueRO.look * 1.5f),
                            direction   = input.ValueRO.look
                        });
                    }

                    if(playerActions.FindAction("ability1").WasPerformedThisFrame())
                    {
				        EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                        {
                            entity      = entity,
                            ability     = "ICEBLAST_1",
                            point       = position + (input.ValueRO.look * 1.5f),
                            direction   = input.ValueRO.look
                        });
                    }

                    if(playerActions.FindAction("melee").WasPerformedThisFrame())
                    {
                        EventQueue.publish(new UseAbilityEvent<Unity.Collections.FixedString64Bytes>
                        {
                            entity      = entity,
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

}
