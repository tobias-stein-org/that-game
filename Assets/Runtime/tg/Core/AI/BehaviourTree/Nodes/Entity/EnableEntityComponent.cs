using Unity.Entities;
using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Entity/Enable Component")]
    public class EnableEntityComponent : Action
    {
        public EntityComponentType compotent;

        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent(context.entity, this.compotent)) { return State.failure; }

            context.entityManager.SetComponentEnabled(context.entity, this.compotent, true);
            return State.success;
        }
    }
}
