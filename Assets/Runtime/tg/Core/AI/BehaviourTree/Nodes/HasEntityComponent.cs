using Unity.Entities;

namespace tg.ai.behaviour.tree.node
{
    public class HasEntityComponent : Action
    {
        public ComponentType    compotent;

        public override State evaluate(ref Context context)
        {
            return context.entityManager.HasComponent(context.entity, this.compotent)
                ? State.success
                : State.failure;
        }
    }
}
