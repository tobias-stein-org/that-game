using Unity.Entities;
using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Entity/Remove Component")]
    public class RemoveEntityComponent : Action
    {
        public EntityComponentType compotent;

        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent(context.entity, this.compotent)) { return State.success; }

            context.entityManager.RemoveComponent(context.entity, this.compotent);
            return State.success;
        }
    }
}
