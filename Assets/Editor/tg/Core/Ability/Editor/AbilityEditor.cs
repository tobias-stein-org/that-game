using System.Linq;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

using Unity.Collections;

namespace tg.editor.ability
{
    using tg.ability;
    using Unity.Entities.UniversalDelegates;

    public class AbilityEditor : EditorWindow
    {
        private const string    ABILITY_DIR     = "Assets/Abilities";
        private const string    ABILITY_LABEL   = "Ability";


        private static void createAbility(string newAbilityFolder, string newAbilityName)
        {
            var activeScene                 = EditorSceneManager.GetActiveScene();
            var abilitiesScene              = openAbilitySubScene();
            {
                AssetDatabase.CreateFolder(AbilityEditor.ABILITY_DIR, newAbilityName);
            
                var description             = ScriptableObject.CreateInstance<AbilityDescription>();

                FixedStringMethods.CopyFrom(ref description.meta.name, newAbilityName);
                FixedStringMethods.CopyFrom(ref description.meta.description, "Description missing.");

                var newAbilityAsset         = $"{newAbilityFolder}/AbilityDesc.asset";
                var newAbilityPrefab        = $"{newAbilityFolder}/AbilityPref.prefab";
                                        
                var newAbilityGO            = new GameObject(newAbilityName);

                description.abilityPrefab   = PrefabUtility.SaveAsPrefabAsset(newAbilityGO, newAbilityPrefab);
                GameObject.DestroyImmediate(newAbilityGO);

                AssetDatabase.CreateAsset(description, newAbilityAsset);
                AssetDatabase.SetLabels(description, new string[] { AbilityEditor.ABILITY_LABEL });

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var abilityGO = new GameObject(newAbilityName);
                {
                    abilityGO.transform.SetParent(null, true);
                    var ability = abilityGO.AddComponent<AbilityAuthering>();
                    {
                        ability.description = description;
                    }
                }
                SceneManager.MoveGameObjectToScene(abilityGO, abilitiesScene);
                EditorSceneManager.MarkSceneDirty(abilitiesScene);


                Selection.activeObject      = description;
            }
            EditorSceneManager.SaveScene(abilitiesScene);
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);
        }

        internal static void deleteAbility(AbilityDescription description)
        {
            var activeScene                 = EditorSceneManager.GetActiveScene();
            var abilitiesScene              = openAbilitySubScene();
            {
                try
                {
                    var abilityGO           = abilitiesScene.GetRootGameObjects().First(GO => GO.name == description.meta.name);
                    GameObject.DestroyImmediate(abilityGO);
                    EditorSceneManager.MarkSceneDirty(abilitiesScene);
                }
                catch(System.Exception e) { UnityEngine.Debug.LogException(e); }
            }
            EditorSceneManager.SaveScene(abilitiesScene);
            EditorSceneManager.OpenScene(activeScene.path, OpenSceneMode.Single);

            try
            {
                AssetDatabase.DeleteAsset(System.IO.Path.GetDirectoryName(AssetDatabase.GetAssetPath(description)));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch(System.Exception e) { UnityEngine.Debug.LogException(e); }
         

            if(EditorWindow.HasOpenInstances<AbilityEditor>())
            {
                var wnd = GetWindow<AbilityEditor>();
                wnd.refreshAbilities();
                wnd.clearInspector();
                
            }
        }

        private static Scene openAbilitySubScene()
        {
            var abilitiesScene  = $"{AbilityEditor.ABILITY_DIR}/abilities.unity";
            var exists          = System.IO.File.Exists(abilitiesScene);

            if(!exists)
            {
                // create new empty scene
                EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene), abilitiesScene);
            }

            return EditorSceneManager.OpenScene(abilitiesScene, OpenSceneMode.Additive);
        }

        /// <summary>
        /// Display a small pop-up dialog with an text field input for the new ability name.
        /// </summary>
        internal class CreateNewAbilityDialog : EditorWindow
        {
            private string newAbilityName   = "New ability name";

            public static void show()
            {
                var window                  = ScriptableObject.CreateInstance<CreateNewAbilityDialog>();

                window.minSize              =
                window.maxSize              = new Vector2(500, 42);
                window.titleContent         = new GUIContent("Create a new ability");

                window.ShowUtility();
            }

            private void CreateGUI()
            {
                this.rootVisualElement.style.marginBottom   =
                this.rootVisualElement.style.marginTop      =
                this.rootVisualElement.style.marginLeft     =
                this.rootVisualElement.style.marginRight    = 4.0f;

                
                var input = new TextField("Name", FixedString64Bytes.UTF8MaxLengthInBytes, false, false, '*');
                {
                    input.SetValueWithoutNotify(this.newAbilityName);
                    input.RegisterCallback((ChangeEvent<string> e) => { this.newAbilityName = e.newValue; });
                    input.Focus();

                    this.rootVisualElement.Add(input);
                }

                var actions = new VisualElement();
                {
                    actions.style.flexDirection = FlexDirection.Row;
                    actions.style.flexGrow      = 1.0f;
                    actions.style.alignItems    = Align.Center;
                    actions.style.justifyContent= Justify.FlexEnd;
                   
                    var cn  = new Button(this.cancel);
                    {
                        cn.text = "Cancel";
                        actions.Add(cn);
                    }

                     var ok  = new Button(this.ok);
                    {
                        ok.text = "OK";
                        actions.Add(ok);
                    }

                    this.rootVisualElement.Add(actions);
                }
            }

            void ok()
            {
                var newAbilityFolder = $"{AbilityEditor.ABILITY_DIR}/{this.newAbilityName}";
                if(AssetDatabase.IsValidFolder(newAbilityFolder))
                {
                    if(EditorUtility.DisplayDialog("Ability already exists", $"Ability '{this.newAbilityName}' already exists. Do you want to choose a different name?", "Yes", "Abort"))
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
                    
                    AbilityEditor.createAbility(newAbilityFolder, this.newAbilityName);
                    this.Close();

                    if(EditorWindow.HasOpenInstances<AbilityEditor>())
                    {
                        GetWindow<AbilityEditor>().refreshAbilities(this.newAbilityName);
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
        internal class AbilityEntry : VisualElement
        {
            public AbilityDescription  abilityDesc
            {
                set
                {
                    this.abilityName                            = value.meta.name.ConvertToString();
                }
            }

            private Label       abilityNameVE;

            public string   abilityName
            {
                get { return this.abilityNameVE.text; }
                set { this.abilityNameVE.text = value; }
            }

            public AbilityEntry()
            {
                var root = new VisualElement();
                {
                    root.style.paddingLeft                      =
                    root.style.paddingRight                     =
                    root.style.paddingTop                       =
                    root.style.paddingBottom                    = 4.0f;

                    this.abilityNameVE                          = new Label();
                    {
                        this.abilityNameVE.style.fontSize       = 14.0f;
                    }
                    root.Add(this.abilityNameVE);
                }

                this.Add(root);
            }
        }

        [SerializeField]
        private VisualTreeAsset     visualTreeAsset = default;

        private VisualElement       leftPanel;
        private VisualElement       rightPanel;
        private InspectorElement    abilityInspector;

        private List<AbilityDescription>   abilities;
        private List<AbilityDescription>   abilitiesFiltered;

        private ToolbarSearchField  search;
        private ListView            abilityList;

        [MenuItem("tg/Ability/Editor")]
        public static void OpenEditorWindow()
        {
            var wnd             = GetWindow<AbilityEditor>();
            wnd.titleContent    = new GUIContent("AbilityEditor");
            wnd.position        = new Rect(0, 0, 800, 600);
        }

        public void CreateGUI()
        {
            var splitView       = new TwoPaneSplitView(0, 300, TwoPaneSplitViewOrientation.Horizontal);
            {
                this.leftPanel  = this.visualTreeAsset.Instantiate();
                {
                    this.leftPanel.Q<ToolbarButton>("create").clicked += () => CreateNewAbilityDialog.show();
                    this.leftPanel.Q<ToolbarSearchField>("search").RegisterValueChangedCallback((ChangeEvent<string> e) => { this.refreshFiltered(e.newValue); });
                }

                this.rightPanel = new VisualElement();
                {
                    this.clearInspector();
                }

                splitView.Add(this.leftPanel);
                splitView.Add(this.rightPanel);
            }
            this.rootVisualElement.Add(splitView);

            this.setupAbilityList();
            this.refreshAbilities();
        }

        internal void clearInspector()
        {
            this.rightPanel.Clear();
            this.abilityInspector = new InspectorElement();
            this.rightPanel.Add(this.abilityInspector);
        }

        internal void setupAbilityList()
        {
            this.abilityList                        = this.leftPanel.Q<ListView>("abilities");
            {
                this.abilityList.makeItem           = () => new AbilityEntry();

                this.abilityList.bindItem           = (ve, i) =>
                {
                    var abilityEntry                = ve as AbilityEntry;
                    abilityEntry.abilityDesc        = this.abilitiesFiltered[i];
                    abilityEntry.userData           = this.abilitiesFiltered[i];
                    abilityEntry.RegisterCallback((MouseDownEvent e) => { this.abilityInspector.Bind(new SerializedObject(this.abilitiesFiltered[i])); });
                };

                this.abilityList.fixedItemHeight    = 28.0f;
            }
        }

        internal void refreshAbilityList()
        {
            this.abilityList.itemsSource = this.abilitiesFiltered;
            this.abilityList.Rebuild();
        }

        internal void refreshAbilities(string select = null)
        {
            this.abilities          = AssetDatabase
                .FindAssets($"l: {AbilityEditor.ABILITY_LABEL}", new string[] { AbilityEditor.ABILITY_DIR })
                .Select(assetGUID   => AssetDatabase.GUIDToAssetPath(assetGUID))
                .Select(assetPath   => (AbilityDescription)AssetDatabase.LoadMainAssetAtPath(assetPath))
                .ToList();
            
            this.refreshFiltered(this.leftPanel.Q<ToolbarSearchField>("search").value);

            if(select != null)
            {
                var index = this.abilitiesFiltered.FindIndex(ability => ability.meta.name.ToString() == select);
                this.abilityList.selectedIndex = index;
                this.abilityInspector.Bind(new SerializedObject(this.abilitiesFiltered[index]));

            }
        }

        internal void refreshFiltered(string filter)
        {
            var search              = filter.ToLower();
            this.abilitiesFiltered  = this.abilities.Where(abilityDesc => string.IsNullOrEmpty(search) || abilityDesc.name.ToLower().Contains(search)).ToList();

            this.refreshAbilityList();
        }
    }
}

