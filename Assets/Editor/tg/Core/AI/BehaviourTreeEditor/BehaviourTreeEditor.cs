using System;

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
            if(tree)
            {
                this.treeView.build(tree);
            }
        }


        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            root.Add(visualTreeAsset.Instantiate());

            this.inspectorView  = root.Q<InspectorView>();
            this.treeView       = root.Q<BehaviourTreeView>();

            // intentionally here to update views after a recompile
            this.OnSelectionChange();
        }
    }



    public static class BehaviourTreeEditorEx
    {
        public static Node createNode(this BehaviourTree bt, Type nodeType, Vector2 position)
        {
            var node            = ScriptableObject.CreateInstance(nodeType) as Node;
            {
                node.id         = Guid.NewGuid();
                node.position   = position;
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
    }
}
