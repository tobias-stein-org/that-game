using System.Linq;

using UnityEditor;
using UnityEngine.UIElements;

namespace tg.editor.application
{
    using tg.application;
    [CustomPropertyDrawer(typeof(tg.application.Tag))]
    public class TagProperty : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var tag             = (Tag)this.fieldInfo.GetValue(property.serializedObject.targetObject);
            var container       = new VisualElement();
            {
                var input       = new DropdownField("Tag");

                input.choices   = tags.tagLookup.Values.ToList();
                input.index     = input.choices.IndexOf(tag.name);
                input.RegisterValueChangedCallback((ChangeEvent<string> e) => this.fieldInfo.SetValue(property.serializedObject.targetObject, new Tag(e.newValue)));
                container.Add(input);
            }

            return container;
        }
    }
}
