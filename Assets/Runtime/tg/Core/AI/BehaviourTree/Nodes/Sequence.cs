using System.Collections.Generic;
using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    [CreateAssetMenu(menuName = "SE")]
    public sealed class Sequence : Node
    {
        public List<Node>   sequence;

        public override State evaluate(ref Context context)
        {
            foreach(var node in this.sequence)
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

        public override void visit(VisitNodeAction action)
        {
            foreach(var node in this.sequence) { node.visit(action); }
        }
    }
}
