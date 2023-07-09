using System.Linq;

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace tg.editor.ai.behaviour.tree
{
    using tg.ai.behaviour.tree;
    using tg.ai.behaviour.tree.node;
    using UnityEngine;

    public class BehaviourTreeView : GraphView
    {
        public class NodeView : UnityEditor.Experimental.GraphView.Node
        {
            private Node node;

            public NodeView(Node node)
            {
                this.node           = node;
                this.viewDataKey    = node.id.ToString();
                this.title          = node.GetType().Name;

                this.style.left     = node.position.x;
                this.style.top      = node.position.y;
            }

            public override void SetPosition(Rect newPos)
            {
                base.SetPosition(newPos);

                this.node.position.x = newPos.x;
                this.node.position.y = newPos.y;
            }

            public static implicit operator Node(NodeView view) { return view.node; }
        }

        public new class UxmlFactory : UxmlFactory<BehaviourTreeView, GraphView.UxmlTraits>
        { }

        private BehaviourTree   tree;

        private Vector2         localMousePosition;

        public BehaviourTreeView()
        {
            this.Insert(0, new GridBackground());
            this.styleSheets.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Editor/tg/Core/AI/BehaviourTreeEditor/BehaviourTreeStyles.uss"));

            this.AddManipulator(new ClickSelector());

            // seems broken on mac
            //this.AddManipulator(new RectangleSelector());

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new ContentZoomer());
        }

        internal void build(BehaviourTree tree)
        {
            this.graphViewChanged -= this.onGraphViewChanged;

            this.DeleteElements(this.graphElements);

            this.tree = tree;
            this.tree.nodes.ForEach(node => this.createNodeView(node));

            this.graphViewChanged += this.onGraphViewChanged;

            this.RegisterCallback<PointerMoveEvent>(e => { this.localMousePosition = e.localPosition; });
        }

        private GraphViewChange onGraphViewChanged(GraphViewChange changes)
        {
            if(changes.elementsToRemove != null)
            {
                changes.elementsToRemove.ForEach(elem =>
                {
                    var node = elem as NodeView;
                    if(node != null)
                    {
                        this.tree.deleteNode(node);
                    }
                });
            }

            return changes;
        }

        private void createNodeView(Node node)
        {
            this.AddElement(new NodeView(node));
        }


        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // default copy, paste ...
            //base.BuildContextualMenu(evt);

            foreach(var nodeType in TypeCache.GetTypesDerivedFrom<Node>().Where(node => node != typeof(Composite) && node != typeof(Decorator)))
            {
                var menuPAth = nodeType.FullName.Remove(0, "tg.ai.behaviour.tree.node.".Length).Replace('.', '/');

                evt.menu.AppendAction(menuPAth, (menuAction) =>
                {
                    var t = -(Vector2)this.viewTransform.position + this.localMousePosition;
                    var s = 1.0f / this.viewTransform.scale.x;
                    
                    this.createNodeView(this.tree.createNode(nodeType, t * s));
                });
            }
        }
    }
}
