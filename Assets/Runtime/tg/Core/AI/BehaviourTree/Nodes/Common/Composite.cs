using System.Collections.Generic;

using UnityEngine;

namespace tg.ai.behaviour.tree.node
{
    public abstract class Composite : Node
    {
        [HideInInspector]
        public List<Node> nodes = new List<Node>();

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