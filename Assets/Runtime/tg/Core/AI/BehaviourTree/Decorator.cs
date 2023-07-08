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

        public override Node clone()
        {
            var instance = Instantiate(this);
            {
                instance.node = this.node.clone();
            }

            return instance;
        }
    }
}
