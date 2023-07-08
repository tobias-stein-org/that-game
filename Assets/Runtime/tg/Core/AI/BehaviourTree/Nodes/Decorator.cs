using System;

namespace tg.ai.behaviour.tree.node
{
    public abstract class Decorator : Node
    {
        public Node node;

        public override void visit(VisitNodeAction action)
        {
           this.node.visit(action);
        }
    }
}
