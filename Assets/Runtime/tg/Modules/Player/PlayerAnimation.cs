using Unity.Entities;

namespace tg.player
{
    using tg.application;
    using UnityEngine;

    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial class PlayerAnimationController : SystemBase
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate(StateManager.state(this));
        }

        protected override void OnUpdate()
        {
            foreach(var (animator, playerInput) in SystemAPI.Query<SystemAPI.ManagedAPI.UnityEngineComponent<Animator>, PlayerInputData>().WithAll<Player>())
            {
                if(playerInput.moveXY.sqrMagnitude > 1e-5f)
                {
                    animator.Value.SetFloat("moveX", playerInput.moveXY.x);
                    animator.Value.SetFloat("moveY", playerInput.moveXY.y);

                    animator.Value.Play("running");
                }
                else
                {
                    animator.Value.Play("idle");
                }

                animator.Value.SetFloat("attack", playerInput.melee);
                animator.Value.SetFloat("cast", playerInput.cast);
            }
        }
    }
}

