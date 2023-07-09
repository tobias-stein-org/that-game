using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Callbacks;

namespace tg.editor.ai.behaviour.tree
{
    using tg.ai.behaviour.tree;
    using tg.ai.behaviour.tree.node;

    public class BehaviourTreeEditor : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset visualTreeAsset = default;

        private BehaviourTreeView   treeView;
        private InspectorView       inspectorView;

        [MenuItem("tg/ai/Behaviour Tree Editor")]
        public static void open()
        {
            var wnd = GetWindow<BehaviourTreeEditor>();
            {
                wnd.titleContent = new GUIContent("Behaviour Tree Editor");
            }
        }

        [OnOpenAsset]
        public static bool onAssetOpen(int instanceId, int line)
        {
            if(Selection.activeObject is BehaviourTree)
            {
                BehaviourTreeEditor.open();
                return true;
            }

            return false;
        }


        public void OnSelectionChange()
        {
            var tree = Selection.activeObject as BehaviourTree;
            if(tree && AssetDatabase.CanOpenAssetInEditor(tree.GetInstanceID()))
            {
                this.treeView.build(tree);
            }
        }

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root              = rootVisualElement;

            root.Add(visualTreeAsset.Instantiate());

            this.inspectorView              = root.Q<InspectorView>();
            this.treeView                   = root.Q<BehaviourTreeView>();
            this.treeView.onNodeSelected    += onNodeSelected;

            // intentionally here to update views after a recompile
            this.OnSelectionChange();
        }

        private void onNodeSelected(Node node)
        {
            this.inspectorView.update(node);
        }
    }


    public static class BehaviourTreeEditorEx
    {
        public static Node createNode(this BehaviourTree bt, Type nodeType, Vector2 position, bool isRoot = false, string name = null)
        {
            var node            = ScriptableObject.CreateInstance(nodeType) as Node;
            {
                node.id         = Guid.NewGuid().ToString();
                node.name       = name ?? nodeType.Name;
                node.position   = position;
                node.isRoot     = isRoot;
            }

            bt.nodes.Add(node);

            AssetDatabase.AddObjectToAsset(node, bt);
            AssetDatabase.SaveAssets();

            return node;
        }

        public static void deleteNode(this BehaviourTree bt, Node node)
        {
            bt.nodes.Remove(node);

            AssetDatabase.RemoveObjectFromAsset(node);
            AssetDatabase.SaveAssets();
        }

        public static void addNode(this BehaviourTree bt, Node parent, Node node)
        {
            if(parent as Decorator != null) { (parent as Decorator).node = node; }
            if(parent as Composite != null) { (parent as Composite).nodes.Add(node); }
        }

        public static void removeNode(this BehaviourTree bt, Node parent, Node node)
        {
            if(parent as Decorator != null) { (parent as Decorator).node = null; }
            if(parent as Composite != null) { (parent as Composite).nodes.Remove(node); }
        }

        public static List<Node> getChildren(this BehaviourTree bt, Node parent)
        {
            if(parent as Decorator != null)
            {
                var decorator = parent as Decorator;
                return decorator.node != null ? new List<Node>() { decorator.node } : new List<Node>(); 
            }

            if(parent as Composite != null) { return (parent as Composite).nodes; }

            return new List<Node>();
        }
    }
}
