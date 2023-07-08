using System.Collections.Generic;

namespace tg.ai.behaviour.tree.node
{
    [UnityEngine.CreateAssetMenu(menuName = "bt/sel")]
    public sealed class Selector : Node
    {
        public List<Node> options;

        public override State evaluate(ref Context context)
        {
            foreach(var node in this.options)
            {
                // skip, until we reach a last running node in the sequence
                if(this.state == State.running && node.state != State.running) { continue; }

                node.state = node.evaluate(ref context);
                switch(node.state)
                {
                    case State.success: return State.success;
                    case State.failure: continue;
                    case State.running: return State.running;
                }
            }

            // no option selected
            return State.failure;
        }

        public override void visit(VisitNodeAction action)
        {
            foreach(var node in this.options) { node.visit(action); }
        }
    }
}
