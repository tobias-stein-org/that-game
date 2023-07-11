using UnityEngine;

namespace tg.ai.behaviour.tree
{
    public delegate void VisitNodeAction(Node node);

    public class NodeContextMenuPathAttribute : System.Attribute
    {
        public string path;

        public NodeContextMenuPathAttribute(string path)
        {
            this.path = path;
        }
    }

    public abstract class Node : ScriptableObject
    {
        #region Behaviour Tree Editor

        [HideInInspector]
        public string   id;

        [HideInInspector]
        public Vector2  position;

        [HideInInspector]
        public bool     isRoot;

        #endregion

        [System.NonSerialized]
        public State    state;

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
