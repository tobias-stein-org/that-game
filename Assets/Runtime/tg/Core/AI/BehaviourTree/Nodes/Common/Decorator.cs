using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    public abstract class Decorator : Node
    {
        [HideInInspector]
        public Node node = null;

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
