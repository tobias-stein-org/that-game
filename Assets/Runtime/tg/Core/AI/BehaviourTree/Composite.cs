using System.Collections.Generic;

namespace tg.ai.behaviour.tree.node
{
    public abstract class Composite : Node
    {
        public List<Node> nodes;

        public override void visit(VisitNodeAction action)
        {
            foreach(var node in this.nodes) { node.visit(action); }
        }

        public override Node clone()
        {
            var instance = Instantiate(this);
            {
                instance.nodes = this.nodes.ConvertAll(node => node.clone());
            }

            return instance;
        }
    }
}