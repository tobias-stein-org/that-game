using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace tg.editor.ai.behaviour.tree
{
    using tg.ai.behaviour.tree;

    [CustomPropertyDrawer(typeof(tg.ai.behaviour.tree.BehaviourTree))]
    public class BehaviourTreeProperty : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            {
                container.Add(new PropertyField(property));

                if(Application.isPlaying && property.objectReferenceValue != null)
                {
                    var openEditor = new Button();
                    {
                        openEditor.text = "Open in Behaviour Tree Editor";
                        openEditor.style.flexGrow = 1.0f;
                        openEditor.clicked += () =>
                        {
                            BehaviourTreeEditor.open(property.objectReferenceValue as BehaviourTree);
                        };
                    }
                    container.Add(openEditor);
                }
            }

            return container;
        }

        // Draw the property inside the given rect
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Using BeginProperty / EndProperty on the parent property means that
            // prefab override logic works on the entire property.
            EditorGUI.BeginProperty(position, label, property);

            if(Application.isPlaying)
            {            
                // Draw label
                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                // Don't make child fields be indented
                var indent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = 0;

                if(GUI.Button(position, "Open in Behaviour Tree Editor"))
                {
                    BehaviourTreeEditor.open(property.objectReferenceValue as BehaviourTree);
                }

                // Set indent back to what it was
                EditorGUI.indentLevel = indent;
            }
            else
            {
                EditorGUI.PropertyField(position, property);
            }

            EditorGUI.EndProperty();
        }
    }
}
