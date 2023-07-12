using Unity.Entities;

namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Entity/Has Component")]
    public class HasEntityComponent : Action
    {
        public EntityComponentType    compotent;

        public override State evaluate(ref Context context)
        {
            return context.entityManager.HasComponent(context.entity, this.compotent)
                ? State.success
                : State.failure;
        }
    }
}
