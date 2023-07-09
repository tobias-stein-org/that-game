using Unity.Entities;
using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    [CreateAssetMenu(menuName = "bt/dis")]
    public class DisableEntityComponent : Action
    {
        public ComponentType    compotent;

        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent(context.entity, this.compotent)) { return State.success; }

            context.entityManager.SetComponentEnabled(context.entity, this.compotent, false);
            return State.success;
        }
    }
}
