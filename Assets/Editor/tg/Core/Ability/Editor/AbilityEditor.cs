using System.Linq;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using UnityEditor.AddressableAssets;

using Unity.Collections;

namespace tg.editor.ability
{
    using tg.ability;

    public class AbilityEditor : EditorWindow
    {
        private const string    ABILITY_DIR                 = "Assets/Abilities";
        private const string    ABILITY_ADDRESSABLE_GROUP   = "Abilities";
        private const string    ABILITY_ADDRESSABLE_PREFIX  = "tg.ability.";

        private static void createAbility(string newAbilityFolder, string newAbilityName)
        {
            AssetDatabase.CreateFolder(AbilityEditor.ABILITY_DIR, newAbilityName);
            
            var description                 = ScriptableObject.CreateInstance<AbilityDescription>();

            FixedStringMethods.CopyFrom(ref description.meta.name, newAbilityName);
            FixedStringMethods.CopyFrom(ref description.meta.description, "Description missing.");

            var newAbilityAsset             = $"{newAbilityFolder}/AbilityDesc.asset";
            var newAbilityPrefab            = $"{newAbilityFolder}/AbilityPref.prefab";
                                    
            var newAbilityGO                = new GameObject(newAbilityName);

            description.abilityPrefab       = PrefabUtility.SaveAsPrefabAsset(newAbilityGO, newAbilityPrefab);
            GameObject.DestroyImmediate(newAbilityGO);

            AssetDatabase.CreateAsset(description, newAbilityAsset);
            AssetDatabase.SetLabels(description, new string[] { AbilityDescription.label });

            var addressableAssetSettings    = AddressableAssetSettingsDefaultObject.Settings;
            var abilitiesGroup              = addressableAssetSettings.FindGroup(ABILITY_ADDRESSABLE_GROUP) ?? addressableAssetSettings.CreateGroup(ABILITY_ADDRESSABLE_GROUP, false, false, false, null, typeof(UnityEditor.AddressableAssets.Settings.GroupSchemas.BundledAssetGroupSchema));
            var adressableEntry             = addressableAssetSettings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(newAbilityAsset), abilitiesGroup);
            adressableEntry.address         = $"{ABILITY_ADDRESSABLE_PREFIX}{newAbilityName}";
            adressableEntry.SetLabel(AbilityDescription.label, true);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject          = description;
        }

        internal static void deleteAbility(AbilityDescription description)
        {
            try
            {
                var assetPath   = AssetDatabase.GetAssetPath(description);

                AssetDatabase.DeleteAsset(System.IO.Path.GetDirectoryName(assetPath));

                var addressableAssetSettings = AddressableAssetSettingsDefaultObject.Settings;
                var abilitiesGroup = addressableAssetSettings.FindGroup(ABILITY_ADDRESSABLE_GROUP);
                if(abilitiesGroup != null)
                {
                    addressableAssetSettings.RemoveAssetEntry(AssetDatabase.AssetPathToGUID(assetPath));
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch(System.Exception e) { UnityEngine.Debug.LogException(e); }
         

            if(EditorWindow.HasOpenInstances<AbilityEditor>())
            {
                var wnd = GetWindow<AbilityEditor>();
                wnd.refreshAbilities();
                Selection.activeObject = null;
            }
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
        private VisualTreeAsset             visualTreeAsset = default;

        private VisualElement               list;

        private List<AbilityDescription>    abilities;
        private List<AbilityDescription>    abilitiesFiltered;

        private ListView                    abilityList;

        [MenuItem("tg/Ability/Editor")]
        public static void OpenEditorWindow()
        {
            var wnd             = GetWindow<AbilityEditor>();
            wnd.titleContent    = new GUIContent("Ability Editor");
            wnd.position        = new Rect(0, 0, 800, 600);
        }

        public void CreateGUI()
        {
            this.list  = this.visualTreeAsset.Instantiate();
            {
                this.list.Q<ToolbarButton>("create").clicked += () => CreateNewAbilityDialog.show();
                this.list.Q<ToolbarSearchField>("search").RegisterValueChangedCallback((ChangeEvent<string> e) => { this.refreshFiltered(e.newValue); });
            }

            this.rootVisualElement.Add(list);

            this.setupAbilityList();
            this.refreshAbilities();
        }

        internal void setupAbilityList()
        {
            this.abilityList                        = this.list.Q<ListView>("abilities");
            {
                this.abilityList.makeItem           = () => new AbilityEntry();

                this.abilityList.bindItem           = (ve, i) =>
                {
                    var abilityEntry                = ve as AbilityEntry;
                    abilityEntry.abilityDesc        = this.abilitiesFiltered[i];
                    abilityEntry.userData           = this.abilitiesFiltered[i];
                    abilityEntry.RegisterCallback((MouseDownEvent e) =>
                    {
                        Selection.activeObject      = this.abilitiesFiltered[i];
                    });
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
                .FindAssets($"l: {AbilityDescription.label}", new string[] { AbilityEditor.ABILITY_DIR })
                .Select(assetGUID   => AssetDatabase.GUIDToAssetPath(assetGUID))
                .Select(assetPath   => (AbilityDescription)AssetDatabase.LoadMainAssetAtPath(assetPath))
                .ToList();
            
            this.refreshFiltered(this.list.Q<ToolbarSearchField>("search").value);

            if(select != null)
            {
                var index = this.abilitiesFiltered.FindIndex(ability => ability.meta.name.ToString() == select);
                this.abilityList.selectedIndex = index;
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

