using UnityEngine;

namespace tg.ai.behaviour.tree
{
    public delegate void VisitNodeAction(Node node);

    public abstract class Node : ScriptableObject
    {

        public State state;

        public virtual void visit(VisitNodeAction action)
        {
            action(this);
        }

        public abstract State evaluate(ref Context context);

        public virtual Node clone()
        {
            return Instantiate(this);
        }
    }
}
