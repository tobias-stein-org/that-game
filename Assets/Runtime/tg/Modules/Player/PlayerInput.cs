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
                input.ValueRW.moveXY  = playerActions.FindAction("move").ReadValue<Vector2>();
                input.ValueRW.melee   = playerActions.FindAction("melee").ReadValue<float>();
                input.ValueRW.cast    = playerActions.FindAction("cast").ReadValue<float>();

                if(playerActions.FindAction("cast").WasPerformedThisFrame())
                {
                    var abilities = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(typeof(AbilityData)).ToEntityArray(Unity.Collections.Allocator.Temp);
			        if(abilities.Length > 0)
			        {
				        EventQueue.publish(new UseAbilityEvent
                        {
                            entity      = player.entity,
                            ability     = abilities[0],
                            point       = (Vector2)transform.Value.position + (input.ValueRO.moveXY * 1.5f),
                            direction   = input.ValueRO.moveXY
                        });
			        }

			        abilities.Dispose();
                }
            }
        }
    }

    public struct PlayerInputData : IComponentData
    {
        public Vector2  moveXY;

        public float    moveSpeed;

        public float    melee;
        public float    cast;
    }
}
