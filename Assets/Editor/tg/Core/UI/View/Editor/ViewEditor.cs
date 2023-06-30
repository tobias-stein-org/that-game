using System.Linq;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.AddressableAssets;

using Unity.Collections;

namespace tg.editor.ui.view
{
    using tg.ui;
    using UnityEditor.UIElements;

    public class ViewEditor : EditorWindow
    {
        public const string VIEW_DIR                = "Assets/UI/Views";

        public const string VIEW_ADDRESSABLE_GROUP  = "Views";
        public const string VIEW_ADDRESSABLE_PREFIX = "tg.ui.view.";


        public static void createNewView(string newViewFolder, string newViewName)
        {
            if(!AssetDatabase.IsValidFolder("Assets/UI")) { AssetDatabase.CreateFolder("Assets", "UI"); }
            if(!AssetDatabase.IsValidFolder("Assets/UI/Views")) { AssetDatabase.CreateFolder("Assets/UI", "Views"); }

            AssetDatabase.CreateFolder(ViewEditor.VIEW_DIR, newViewName);

            var newViewSO   = $"{newViewFolder}/View.asset";
            var newViewUXML = $"{newViewFolder}/View.uxml";
            var newViewCtrl = $"{newViewFolder}/Controller.cs";

            System.IO.File.WriteAllText(newViewUXML, @"<?xml version=""1.0"" encoding=""utf-8""?>
<engine:UXML xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:engine=""UnityEngine.UIElements"" xmlns:editor=""UnityEditor.UIElements"">
</engine:UXML>");

            System.IO.File.WriteAllText(newViewCtrl, @$"using UnityEngine.UIElements;

namespace tg.ui.view
{{
    public class {newViewName}Controller : IViewController
    {{
        public void activated(VisualElement view)
        {{
        }}

        public void deactivated()
        {{
        }}
    }}
}}");

            AssetDatabase.ImportAsset(newViewCtrl, ImportAssetOptions.Default);
            AssetDatabase.ImportAsset(newViewUXML, ImportAssetOptions.Default);

            var view        = ScriptableObject.CreateInstance<View>();
            var uxml        = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(newViewUXML);

            view.name       = newViewName;
            view.view       = uxml;
            view.controller = $"tg.ui.view.{newViewName}Controller, tg.runtime, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null";

            AssetDatabase.CreateAsset(view, newViewSO);

            AssetDatabase.SetLabels(view, new string[] { View.label });

            var addressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
            var viewsGroup = addressableAssetSettings.FindGroup(VIEW_ADDRESSABLE_GROUP) ?? addressableAssetSettings.CreateGroup(VIEW_ADDRESSABLE_GROUP, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            var adressableEntry = addressableAssetSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(newViewSO), viewsGroup);

            adressableEntry.address = $"{VIEW_ADDRESSABLE_PREFIX}{view.name}";
            adressableEntry.SetLabel(View.label, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = view;
        }

        internal static void deleteView(View view)
        {
            try
            {
                var assetPath = AssetDatabase.GetAssetPath(view);

                AssetDatabase.DeleteAsset(System.IO.Path.GetDirectoryName(assetPath));

                var addressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
                var viewsGroup = addressableAssetSettings.FindGroup(VIEW_ADDRESSABLE_GROUP);
                if(viewsGroup != null)
                {
                    addressableAssetSettings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch(System.Exception e) { UnityEngine.Debug.LogException(e); }


            if(EditorWindow.HasOpenInstances<ViewEditor>())
            {
                var wnd = GetWindow<ViewEditor>();
                wnd.refreshViews();
                Selection.activeObject = null;
            }
        }


        /// <summary>
        /// Display a small pop-up dialog with an text field input for the new view name.
        /// </summary>
        internal class CreateNewViewDialog : EditorWindow
        {
            private string newViewName = "New view name";

            public static void show()
            {
                var window = ScriptableObject.CreateInstance<CreateNewViewDialog>();

                window.minSize =
                window.maxSize = new Vector2(500, 42);
                window.titleContent = new GUIContent("Create a new view");

                window.ShowUtility();
            }

            private void CreateGUI()
            {
                this.rootVisualElement.style.marginBottom =
                this.rootVisualElement.style.marginTop =
                this.rootVisualElement.style.marginLeft =
                this.rootVisualElement.style.marginRight = 4.0f;


                var input = new TextField("Name", FixedString64Bytes.UTF8MaxLengthInBytes, false, false, '*');
                {
                    input.SetValueWithoutNotify(this.newViewName);
                    input.RegisterCallback((ChangeEvent<string> e) => { this.newViewName = e.newValue; });
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

                    this.rootVisualElement.Add(actions);
                }
            }

            void ok()
            {
                var newViewFolder = $"{ViewEditor.VIEW_DIR}/{this.newViewName}";
                if(AssetDatabase.IsValidFolder(newViewFolder))
                {
                    if(EditorUtility.DisplayDialog("View already exists", $"View '{this.newViewName}' already exists. Do you want to choose a different name?", "Yes", "Abort"))
                    {
                        return;
                    }
                    else
                    {
                        this.Close();
                    }
                }
                else
                {

                    ViewEditor.createNewView(newViewFolder, this.newViewName);
                    this.Close();

                    if(EditorWindow.HasOpenInstances<ViewEditor>())
                    {
                        GetWindow<ViewEditor>().refreshViews(this.newViewName);
                    }
                }
            }

            void cancel()
            {
                this.Close();
            }
        }

        /// <summary>
        /// Visual element for the abilities list view.
        /// </summary>
        internal class ViewEntry : VisualElement
        {
            public View view
            {
                set
                {
                    this.abilityName = value.name;
                }
            }

            private Label viewNameVE;

            public string abilityName
            {
                get { return this.viewNameVE.text; }
                set { this.viewNameVE.text = value; }
            }

            public ViewEntry()
            {
                var root = new VisualElement();
                {
                    root.style.paddingLeft =
                    root.style.paddingRight =
                    root.style.paddingTop =
                    root.style.paddingBottom = 4.0f;

                    this.viewNameVE = new Label();
                    {
                        this.viewNameVE.style.fontSize = 14.0f;
                    }
                    root.Add(this.viewNameVE);
                }

                this.Add(root);
            }
        }



        [SerializeField]
        private VisualTreeAsset             visualTreeAsset = default;

        private VisualElement               list;

        private List<View>                  views;
        private List<View>                  viewsFiltered;

        private ListView                    viewsList;

        [MenuItem("tg/UI/Editor")]
        public static void openEditorWindow()
        {
            ViewEditor wnd      = GetWindow<ViewEditor>();
            wnd.titleContent    = new GUIContent("View Editor");
            wnd.position        = new Rect(0, 0, 800, 600);

        }

        public void CreateGUI()
        {
            this.list = this.visualTreeAsset.Instantiate();
            {
                this.list.Q<ToolbarButton>("create").clicked += () => CreateNewViewDialog.show();
                this.list.Q<ToolbarSearchField>("search").RegisterValueChangedCallback((ChangeEvent<string> e) => { this.refreshFiltered(e.newValue); });
            }

            this.rootVisualElement.Add(list);

            this.setupViewList();
            this.refreshViews();
        }

        internal void setupViewList()
        {
            this.viewsList = this.list.Q<ListView>("views");
            {
                this.viewsList.makeItem = () => new ViewEntry();

                this.viewsList.bindItem = (ve, i) =>
                {
                    var abilityEntry = ve as ViewEntry;
                    abilityEntry.view = this.viewsFiltered[i];
                    abilityEntry.userData = this.viewsFiltered[i];
                    abilityEntry.RegisterCallback((MouseDownEvent e) =>
                    {
                        Selection.activeObject = this.viewsFiltered[i];
                    });
                };

                this.viewsList.fixedItemHeight = 28.0f;
            }
        }

        internal void refreshViewList()
        {
            this.viewsList.itemsSource = this.viewsFiltered;
            this.viewsList.Rebuild();
        }

        internal void refreshViews(string select = null)
        {
            this.views = AssetDatabase
                .FindAssets($"l: {View.label}", new string[] { ViewEditor.VIEW_DIR })
                .Select(assetGUID => AssetDatabase.GUIDToAssetPath(assetGUID))
                .Select(assetPath => (View)AssetDatabase.LoadMainAssetAtPath(assetPath))
                .ToList();

            this.refreshFiltered(this.list.Q<ToolbarSearchField>("search").value);

            if(select != null)
            {
                var index = this.viewsFiltered.FindIndex(view => view.name.ToString() == select);
                this.viewsList.selectedIndex = index;
            }
        }

        internal void refreshFiltered(string filter)
        {
            var search = filter.ToLower();
            this.viewsFiltered = this.views.Where(abilityDesc => string.IsNullOrEmpty(search) || abilityDesc.name.ToLower().Contains(search)).ToList();

            this.refreshViewList();
        }
    }
}
