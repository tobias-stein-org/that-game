namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Logic/Invert")]
    public sealed class Invert : Decorator
    {
        public override State evaluate(ref Context context)
        {
            node.state = node.evaluate(ref context);
            switch(node.state)
            {
                case State.success: return State.failure;
                case State.failure: return State.success;
            }

            return State.running;
        }
    }
}
