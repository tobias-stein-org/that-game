namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Steering/Wander")]
    public class Wander : Action
    {
        public override State evaluate(ref Context context)
        {
            if(!context.entityManager.HasComponent<tg.ai.steering.behaviour.entities.Wander>(context.entity)) { return State.failure; }

            context.entityManager.SetComponentEnabled<tg.ai.steering.behaviour.entities.Wander>(context.entity, true);
            return State.success;
        }
    }
}
