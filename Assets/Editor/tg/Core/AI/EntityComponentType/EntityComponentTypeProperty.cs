using System.Linq;

using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Unity.Entities;

namespace tg.editor.ai.behaviour
{
    [CustomPropertyDrawer(typeof(tg.ai.behaviour.tree.EntityComponentType))]
    public class EntityComponentTypeProperty : PropertyDrawer
    {

        private static readonly TypeManager.TypeCategory[] typeCategoryFilter = new TypeManager.TypeCategory[]
        {
            TypeManager.TypeCategory.ComponentData,
            TypeManager.TypeCategory.BufferData,
            TypeManager.TypeCategory.ISharedComponentData
        };

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var type = (tg.ai.behaviour.tree.EntityComponentType)this.fieldInfo.GetValue(property.serializedObject.targetObject);
            var container = new VisualElement();
            {
                container.style.flexDirection = FlexDirection.Row;
                container.style.alignContent = Align.Center;

                var label = new Label("Component");
                label.style.alignContent = Align.Center;

                var input = new ToolbarPopupSearchField();
                input.style.flexGrow = 1.0f;

                var currnet = (tg.ai.behaviour.tree.EntityComponentType)this.fieldInfo.GetValue(property.serializedObject.targetObject);
                foreach(var componentType in TypeManager.AllTypes.Where(type => type.Type != null && typeCategoryFilter.Contains(type.Category)))
                {
                    if(currnet.type == componentType.Type.AssemblyQualifiedName)
                    {
                        input.value = componentType.Type.Name;
                    }

                    input.menu.AppendAction(componentType.Type.Name, (action) =>
                    {
                        input.value = componentType.Type.Name;
                        this.fieldInfo.SetValue(property.serializedObject.targetObject, new tg.ai.behaviour.tree.EntityComponentType(componentType.Type));
                        property.serializedObject.ApplyModifiedProperties();
                    });

                    input.Q<Button>("unity-cancel").clicked += () =>
                    {
                        this.fieldInfo.SetValue(property.serializedObject.targetObject, new tg.ai.behaviour.tree.EntityComponentType(null));
                        property.serializedObject.ApplyModifiedProperties();
                    };

                    input.RegisterCallback((FocusInEvent e) => { input.ShowMenu(); });
                }

                container.Add(label);
                container.Add(input);
            }

            return container;
        }
    }
}
