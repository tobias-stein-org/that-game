using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace tg.editor.level
{
    using System.Drawing.Printing;
    using tg.level;

    [CustomEditor(typeof(Module))]
    public class ModulePropertyDrawer : Editor
    {
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement inspector = new VisualElement();

            var module = this.target as Module;
            if(module)
            {
                var constructionTypeField = new EnumField("Construction Type", LevelData.Tile.ConstructionType.Walkable);
                constructionTypeField.bindingPath = "constructionType";

                var weightField = new FloatField("Weight");
                weightField.bindingPath = "weight";

                var spriteField = new ObjectField("Sprite");
                spriteField.objectType  = typeof(Sprite);
                spriteField.value       = module.sprite;
               

                var spritePreviewField = new VisualElement();

                const int previewSize = 128;
                spritePreviewField.style.width = previewSize;
                spritePreviewField.style.height = previewSize;
                spritePreviewField.style.marginLeft = -previewSize;
                spritePreviewField.style.marginTop = 4;
                spritePreviewField.style.right = new StyleLength(Length.Percent(-100.0f));

                spritePreviewField.style.backgroundImage = Background.FromSprite(module.sprite);

                spriteField.RegisterValueChangedCallback((ChangeEvent<Object> e) =>
                {
                    module.sprite = (Sprite)e.newValue;
                    spritePreviewField.style.backgroundImage = Background.FromSprite((Sprite)e.newValue);
                });

                inspector.Add(constructionTypeField);
                inspector.Add(weightField);
                inspector.Add(spriteField);
                inspector.Add(spritePreviewField);
            }

            return inspector;
        }

        public override void OnInspectorGUI()
        {
                Debug.Log("draw");
            base.OnInspectorGUI();
        }
        //{
        //    var module = this.target as Module;
        //    if(module && module.sprite && module.sprite.texture)
        //    {
        //        EditorGUI.DrawPreviewTexture(module.sprite.textureRect, module.sprite.texture);
        //    } 
        //}
    }
}
