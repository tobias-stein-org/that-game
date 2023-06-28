using Unity.Entities;
using Unity.Mathematics;

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
                animator.Value.SetFloat("moveX", playerInput.look.x);
                animator.Value.SetFloat("moveY", playerInput.look.y);

                if(math.lengthsq(playerInput.move) > 1e-5f)
                {

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

