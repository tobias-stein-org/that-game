namespace tg.ai.behaviour.tree.node
{
    [NodeContextMenuPath("Logic/Sequence")]
    public sealed class Sequence : Composite
    {
        public override State evaluate(ref Context context)
        {
            foreach(var node in this.nodes)
            {
                // skip, until we reach a last running node in the sequence
                if(this.state == State.running && node.state != State.running) { continue; }

                node.state = node.evaluate(ref context);
                switch(node.state)
                {
                    case State.success: continue;
                    case State.failure: return State.failure;
                    case State.running: return State.running;
                }
            }

            return State.success;
        }
    }
}
