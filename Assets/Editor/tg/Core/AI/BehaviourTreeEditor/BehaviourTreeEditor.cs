using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Callbacks;

namespace tg.editor.ai.behaviour.tree
{
    using System.Text.RegularExpressions;
    using GluonGui.WorkspaceWindow.Views.WorkspaceExplorer;
    using tg.ability;
    using tg.ai.behaviour.tree;
    using tg.ai.behaviour.tree.node;
    using tg.editor.ability;
    using Unity.Collections;
    using UnityEditor.AddressableAssets;
    using static tg.ai.entities.Sensor;

    public class BehaviourTreeEditor : EditorWindow
    {
        private const string BEHAVIOURTREE_DIR                  = "Assets/AI/BehaviourTrees";
        private const string BEHAVIOURTREE_ADDRESSABLE_GROUP    = "BehaviourTrees";
        private const string BEHAVIOURTREE_ADDRESSABLE_PREFIX   = "tg.ai.behaviour.tree.";

        /// <summary>
        /// Display a small pop-up dialog with an text field input.
        /// </summary>
        internal class CreateNewBehaviourTreeDialog : EditorWindow
        {
            private const string pattern = @"^(?![_\d])[\w]+$";
            private string newBehaviourTreeName = "NEW_BEHAVIOUR_TREE";

            public static void show()
            {
                var window = ScriptableObject.CreateInstance<CreateNewBehaviourTreeDialog>();

                window.minSize =
                window.maxSize = new Vector2(500, 42);
                window.titleContent = new GUIContent("Create a new Behaviour Tree");

                window.ShowUtility();
            }

            private void CreateGUI()
            {
                this.rootVisualElement.style.marginBottom =
                this.rootVisualElement.style.marginTop =
                this.rootVisualElement.style.marginLeft =
                this.rootVisualElement.style.marginRight = 4.0f;


                var input = new TextField("Name");
                {
                    input.SetValueWithoutNotify(this.newBehaviourTreeName);
                    input.Focus();

                    this.rootVisualElement.Add(input);
                }

                var actions = new VisualElement();
                {
                    actions.style.flexDirection = FlexDirection.Row;
                    actions.style.flexGrow = 1.0f;
                    actions.style.alignItems = Align.Center;
                    actions.style.justifyContent = Justify.FlexEnd;

                    var cn = new Button(this.cancel);
                    {
                        cn.text = "Cancel";
                        actions.Add(cn);
                    }

                    var ok = new Button(this.ok);
                    {
                        ok.text = "OK";
                        actions.Add(ok);
                    }

                    input.RegisterCallback((ChangeEvent<string> e) =>
                    {
                        this.newBehaviourTreeName = e.newValue;
                        ok.SetEnabled(Regex.IsMatch(e.newValue, pattern));
                    });

                    this.rootVisualElement.Add(actions);
                }
            }

            void ok()
            {
                var assetPath = $"{BehaviourTreeEditor.BEHAVIOURTREE_DIR}/{this.newBehaviourTreeName}.asset";
                if(System.IO.File.Exists(assetPath))
                {
                    if(EditorUtility.DisplayDialog("Behviour Tree with this name already exists", $"Behaviour Tree '{this.newBehaviourTreeName}' already exists. Do you want to choose a different name?", "Yes", "Abort"))
                    {
                        return;
                    }
                }
                else
                {
                    if(!AssetDatabase.IsValidFolder("Assets/AI")) { AssetDatabase.CreateFolder("Assets", "AI"); }
                    if(!AssetDatabase.IsValidFolder("Assets/AI/BehaviourTrees")) { AssetDatabase.CreateFolder("Assets/AI", "BehaviourTrees"); }


                    var asset = ScriptableObject.CreateInstance<BehaviourTree>();
                    {
                        asset.name = this.newBehaviourTreeName;

                        AssetDatabase.CreateAsset(asset, assetPath);
                        AssetDatabase.SetLabels(asset, new string[] { BehaviourTree.label });

                        var addressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
                        var behaviourTreesGroup = addressableAssetSettings.FindGroup(BehaviourTreeEditor.BEHAVIOURTREE_ADDRESSABLE_GROUP) ?? addressableAssetSettings.CreateGroup(BehaviourTreeEditor.BEHAVIOURTREE_ADDRESSABLE_GROUP, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
                        var adressableEntry = addressableAssetSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(assetPath), behaviourTreesGroup);
                        adressableEntry.address = $"{BehaviourTreeEditor.BEHAVIOURTREE_ADDRESSABLE_PREFIX}{this.newBehaviourTreeName}";
                        adressableEntry.SetLabel(BehaviourTree.label, true);
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    Selection.activeObject = asset;
                }

                this.Close();
            }

            void cancel()
            {
                this.Close();
            }
        }

        internal static void deleteBehaviourTree(BehaviourTree asset)
        {
            if(EditorUtility.DisplayDialog("Delete Behaviour Tree", $"Do your really want to delete the '{asset.name}' Behaviour Tree?", "Delete", "Abort"))
            {
                try
                {
                    var assetPath = AssetDatabase.GetAssetPath(asset);
                    AssetDatabase.DeleteAsset(assetPath);

                    var addressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
                    var behaviourTreeGroup = addressableAssetSettings.FindGroup(BehaviourTreeEditor.BEHAVIOURTREE_ADDRESSABLE_GROUP);
                    if(behaviourTreeGroup != null)
                    {
                        addressableAssetSettings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
                    }

                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
                catch(System.Exception e) { UnityEngine.Debug.LogException(e); }
            }
        }

        [SerializeField]
        private VisualTreeAsset visualTreeAsset = default;

        private BehaviourTreeView   treeView;
        private InspectorView       inspectorView;

        [MenuItem("tg/ai/Behaviour Tree Editor")]
        public static void open(BehaviourTree tree = null)
        {
            var wnd = GetWindow<BehaviourTreeEditor>();
            {
                wnd.titleContent = new GUIContent("Behaviour Tree Editor");
                wnd.openTree(tree);
            }
        }

        [OnOpenAsset]
        public static bool onAssetOpen(int instanceId, int line)
        {
            if(Selection.activeObject is BehaviourTree)
            {
                BehaviourTreeEditor.open(Selection.activeObject as BehaviourTree);
                return true;
            }

            return false;
        }


        public void OnSelectionChange()
        {
            var tree = Selection.activeObject as BehaviourTree;
            this.openTree(tree);
        }

        internal void openTree(BehaviourTree tree)
        {
            if(tree && (Application.isPlaying || AssetDatabase.CanOpenAssetInEditor(tree.GetInstanceID())))
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

            this.buildMenu(root.Q<ToolbarMenu>("menu"));

            root.Q<ToolbarSearchField>("search").RegisterValueChangedCallback((ChangeEvent<string> e) =>
            {
                if(string.IsNullOrEmpty(e.newValue))
                {
                    this.treeView.nodes.ForEach(node => node.style.opacity = 1.0f);
                    return;
                }

                var search = e.newValue.ToLower();
                this.treeView.nodes.ForEach(node => node.style.opacity = node.name.ToLower().Contains(search) ? 1.0f : 0.25f);
            });
        }

        private void buildMenu(ToolbarMenu menu)
        {
            menu.menu.AppendAction("Open Behaviour Tree", action =>
            {
                var path = EditorUtility.OpenFilePanel("Open Behaviour Tree", BehaviourTreeEditor.BEHAVIOURTREE_DIR, "asset");
                if(!string.IsNullOrEmpty(path))
                {
                    try
                    {
                        var assetPath = path.Split("Assets/")[1];
                        BehaviourTree tree = AssetDatabase.LoadAssetAtPath<BehaviourTree>($"Assets/{assetPath}");
                        if(tree != null)
                        {
                            this.treeView.build(tree);
                        }
                    }
                    catch
                    {
                        UnityEngine.Debug.LogError($"'{path}' is not a Unity asset path.");
                    }
                }

            }, DropdownMenuAction.AlwaysEnabled);
            menu.menu.AppendAction("Create new Behaviour Tree", action => CreateNewBehaviourTreeDialog.show(), DropdownMenuAction.AlwaysEnabled);
            menu.menu.AppendSeparator();
            menu.menu.AppendAction("Delete Behaviour Tree", action =>
            {
                if(Selection.activeObject != null && (Selection.activeObject as BehaviourTree) != null)
                {
                    BehaviourTreeEditor.deleteBehaviourTree(Selection.activeObject as BehaviourTree);
                }
            }, DropdownMenuAction.AlwaysEnabled);
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

            Undo.RecordObject(bt, $"BehaviourTreeEditor.createNode({node.id})");
            bt.nodes.Add(node);

            AssetDatabase.AddObjectToAsset(node, bt);
            Undo.RegisterCreatedObjectUndo(node, $"BehaviourTreeEditor.createNode({node.id})");
            AssetDatabase.SaveAssets();

            return node;
        }

        public static void deleteNode(this BehaviourTree bt, Node node)
        {
            Undo.RecordObject(bt, $"BehaviourTreeEditor.deleteNode({node.id})");
            bt.nodes.Remove(node);

            //AssetDatabase.RemoveObjectFromAsset(node);
            Undo.DestroyObjectImmediate(node);

            AssetDatabase.SaveAssets();
        }

        public static void addNode(this BehaviourTree bt, Node parent, Node node)
        {
            if(parent as Decorator != null)
            {
                Undo.RecordObject(parent, "BehaviourTreeEditor.addNode()");
                (parent as Decorator).node = node;
                EditorUtility.SetDirty(parent);
            }
            if(parent as Composite != null)
            {
                Undo.RecordObject(parent, "BehaviourTreeEditor.addNode()");
                (parent as Composite).nodes.Add(node);
                EditorUtility.SetDirty(parent);
            }
        }

        public static void removeNode(this BehaviourTree bt, Node parent, Node node)
        {
            if(parent as Decorator != null)
            {
                Undo.RecordObject(parent, "BehaviourTreeEditor.removeNode()");
                (parent as Decorator).node = null;
                EditorUtility.SetDirty(parent);
            }
            if(parent as Composite != null)
            {
                Undo.RecordObject(parent, "BehaviourTreeEditor.removeNode()");
                (parent as Composite).nodes.Remove(node);
                EditorUtility.SetDirty(parent);
            }
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
