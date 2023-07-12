
namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Entity/Disable Component")]
    public class DisableEntityComponent : Action
    {
        public EntityComponentType compotent;

        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent(context.entity, this.compotent)) { return State.success; }

            context.entityManager.SetComponentEnabled(context.entity, this.compotent, false);
            return State.success;
        }
    }
}
