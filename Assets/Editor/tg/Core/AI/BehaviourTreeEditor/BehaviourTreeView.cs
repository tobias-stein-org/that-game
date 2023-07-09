using System.Linq;

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

namespace tg.editor.ai.behaviour.tree
{
    using System.Collections.Generic;
    using tg.ai.behaviour.tree;
    using tg.ai.behaviour.tree.node;
    using UnityEngine;

    public class BehaviourTreeView : GraphView
    {
        public delegate void NodeSelected(Node view);

        public class NodeView : UnityEditor.Experimental.GraphView.Node
        {
            private  readonly Node  node;

            internal Port           input;
            internal Port           output;

            public   NodeSelected   onSelected;

            public NodeView(Node node)
            {
                this.node           = node;
                this.viewDataKey    = node.id;
                this.title          = node.name;

                this.style.left     = node.position.x;
                this.style.top      = node.position.y;

                this.createInputs(node);
                this.createOutputs(node);
            }

            private void createInputs(Node node)
            {
                if(this.node.isRoot) { return; }

                this.input = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(bool));

                this.input.portName = "";
                this.inputContainer.Add(this.input);
            }

            private void createOutputs(Node node)
            {
                if(node is Composite)
                {
                    this.output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(bool));
                }
                else if(node is Decorator)
                {
                    this.output = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                }

                if(this.output != null)
                {
                    this.output.portName = "";
                    this.inputContainer.Add(this.output);
                }
            }

            public override void SetPosition(Rect newPos)
            {
                base.SetPosition(newPos);

                this.node.position.x = newPos.x;
                this.node.position.y = newPos.y;
            }

            public override void OnSelected()
            {
                base.OnSelected();
                this.onSelected?.Invoke(this.node);
            }

            public static implicit operator Node(NodeView view) { return view.node; }
        }

        public new class UxmlFactory : UxmlFactory<BehaviourTreeView, GraphView.UxmlTraits>
        { }

        public  NodeSelected                onNodeSelected;

        private BehaviourTree               tree;

        private Vector2                     localMousePosition;

        private static readonly System.Type rootNodeType = typeof(tg.ai.behaviour.tree.node.Selector);

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

            // create root
            if(this.tree.root == null)
            {
                this.tree.root = this.tree.createNode(rootNodeType, Vector2.zero, true, this.tree.name);
                EditorUtility.SetDirty(this.tree);
                AssetDatabase.SaveAssets();
            }

            // create nodes 
            this.tree.nodes.ForEach(node => this.createNodeView(node));
            // create edges
            this.tree.nodes.ForEach(node => this.createEdges(node));

            this.graphViewChanged += this.onGraphViewChanged;

            this.RegisterCallback<PointerMoveEvent>(e => { this.localMousePosition = e.localPosition; });
        }

   
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            return this.ports.Where(endPort => endPort.direction != startPort.direction && endPort.node != startPort.node).ToList();
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

                    var edge = elem as Edge;
                    if(edge != null)
                    {
                        var parent = edge.output.node as NodeView;
                        var child = edge.input.node as NodeView;

                        this.tree.removeNode(parent, child);
                    }
                });
            }

            if(changes.edgesToCreate != null)
            {
                changes.edgesToCreate.ForEach(edge =>
                {
                    var parent = edge.output.node as NodeView;
                    var child  = edge.input.node as NodeView;

                    this.tree.addNode(parent, child);
                });
            }

            return changes;
        }

        private void createNodeView(Node node)
        {
            var view        = new NodeView(node);
            view.onSelected = this.onNodeSelected;

            this.AddElement(view);
        }

        private void createEdges(Node parent)
        {
            var parentNodeView      = this.GetNodeByGuid(parent.id) as NodeView;
            this.tree.getChildren(parent).ForEach(child =>
            {
                var childNodeView   = this.GetNodeByGuid(child.id) as NodeView;
                var edge            = parentNodeView.output.ConnectTo(childNodeView.input);
                this.AddElement(edge);
            });
        }

        private static readonly List<System.Type> rootNodeTypes = new List<System.Type>
        {
            typeof(tg.ai.behaviour.tree.node.Action),
            typeof(tg.ai.behaviour.tree.node.Composite),
            typeof(tg.ai.behaviour.tree.node.Decorator),
        };
        
        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            // default copy, paste ...
            //base.BuildContextualMenu(evt);

            foreach(var nodeType in TypeCache.GetTypesDerivedFrom<Node>().Where(node => !rootNodeTypes.Contains(node)))
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
