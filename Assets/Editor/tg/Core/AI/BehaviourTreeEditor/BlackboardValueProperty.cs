using System.Reflection;

using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

using Unity.Collections;


namespace tg.editor.ai.behaviour.tree
{
    [CustomPropertyDrawer(typeof(tg.ai.behaviour.tree.BlackboardValue<>))]
    public class BlackboardValueProperty : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var bbv             = this.fieldInfo.GetValue(property.serializedObject.targetObject);

            var bbvType         = this.fieldInfo.FieldType;
            var bbvGenType      = bbvType.GenericTypeArguments[0];

            var keyField        = bbvType.GetField("key", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var keyValue        = keyField.GetValue(bbv) as string;

            var container       = new VisualElement();
            {
                var input       = new TextField(property.displayName, FixedString64Bytes.UTF8MaxLengthInBytes, false, false, '*');

                input.userData  = typeof(tg.ai.behaviour.tree.BlackboardValue<>);
                input.SetValueWithoutNotify(keyValue);
                input.RegisterValueChangedCallback((ChangeEvent<string> e) =>
                {
                    keyField.SetValue(bbv, e.newValue);
                    property.serializedObject.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                });
                input.style.backgroundColor = new Color(0.8f, 0.2f, 0.0f, 0.7f);
                if(string.IsNullOrWhiteSpace(keyValue))
                {
                    // if key not set yet, use key specified by BlackboardValueAttribute or fallback to field name
                    var attr    = this.fieldInfo.GetCustomAttribute<tg.ai.behaviour.tree.BlackboardValueAttribute>();
                    input.value = attr != null ? attr.name : this.fieldInfo.Name;
                }

                container.Add(input);
            }

            return container;
        }
    }
}
