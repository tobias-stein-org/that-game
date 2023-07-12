using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace tg.editor.ai.behaviour.tree
{
    using tg.ai.behaviour.tree;

    public class InspectorView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<InspectorView, VisualElement.UxmlTraits>
        { }

        public void update(Node node)
        {
            // clear old inspector view 
            this.Clear();

            if(node == null) { return; }

            var so = new SerializedObject(node);
            foreach(var field in node.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
            {
                if(System.Attribute.GetCustomAttribute(field, typeof(HideInInspector)) as HideInInspector != null) { continue; }

                var property            = so.FindProperty(field.Name);
                if(property != null)
                {
                    var propertyField       = new PropertyField();
                    {
                        propertyField.label = property.displayName;
                        propertyField.BindProperty(property);
                    }
                    this.Add(propertyField);
                }
            }
        }
    }
}
