using System.Linq;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Unity.Collections;

namespace tg.editor.ability
{
    using tg.ability;

    [CustomEditor(typeof(AbilityDescription))]
    public class AbilityDescriptionInspector : Editor
    {
        [SerializeField]
        private VisualTreeAsset visualTreeAsset = default;

        private VisualElement   root;

        private new AbilityDescription target { get { return base.target as AbilityDescription; } }

        public override VisualElement CreateInspectorGUI()
        {
            this.root   = new VisualElement();
            var ui      = this.visualTreeAsset.Instantiate();

            ui.Q<TextField>("name").SetValueWithoutNotify(this.target.meta.name.ToString());
            var desc    = ui.Q<TextField>("description");
            {
                desc.maxLength = FixedString128Bytes.UTF8MaxLengthInBytes;
                desc.SetValueWithoutNotify(this.target.meta.description.ToString());
                desc.RegisterValueChangedCallback((ChangeEvent<string> e) => { FixedStringMethods.CopyFrom(ref this.target.meta.description, e.newValue); });
            }

            ui.Q<VisualElement>("actions").Q<Button>("delete").clicked += this.deleteAbility;

            this.root.Add(ui);
            this.root.Add(this.createAbilityPropertView());

            return this.root;
        }

        private VisualElement createAbilityPropertView()
        {
            Foldout view = new Foldout();
            {
                view.text   = "Properties";

                foreach(var comp in this.target.abilityPrefab.GetComponentsInChildren<Component>())
                {
                    var so = new SerializedObject(comp);
                    var ap = new List<PropertyField>();

                    foreach(var field in comp.GetType().GetFields().Where(f => f.IsDefined(typeof(AbilityPropertyAttribute), false)))
                    {
                        var abilityProperty = new PropertyField();
                        {
                            abilityProperty.BindProperty(so.FindProperty(field.Name));
                        }
                        ap.Add(abilityProperty);
                    }

                    if(ap.Count > 0)
                    {
                        Foldout properties  = new Foldout();
                        {
                            properties.text = comp.GetType().Name;
                            foreach(var prop in ap)
                            {
                                properties.Add(prop);
                            }
                        }

                        view.Add(properties);
                    }
                }
            }

            if(view.childCount == 0)
            {
                
                var hint = new Label("Hint");
                {
                    hint.text = $"No '{typeof(AbilityPropertyAttribute).Name}' annotated fields found.";
                }

                view.Add(hint);
            }

            return view;
        }

        private void deleteAbility()
        {
            if(EditorUtility.DisplayDialog("Delete Ability", $"Do your really want to delete the '{this.target.meta.name}' ability?", "Delete", "Abort"))
            {
                AbilityEditor.deleteAbility(this.target);
            }
        }
    }
}
