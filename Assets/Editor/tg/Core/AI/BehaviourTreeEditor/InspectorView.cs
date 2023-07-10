using UnityEngine.UIElements;
using UnityEditor;

namespace tg.editor.ai.behaviour.tree
{
    using tg.ai.behaviour.tree;

    public class InspectorView : VisualElement
    {
        public new class UxmlFactory : UxmlFactory<InspectorView, VisualElement.UxmlTraits>
        { }

        private Editor editor;

        public void update(Node node)
        {
            // clear old inspector view 
            this.Clear();
            UnityEngine.Object.DestroyImmediate(this.editor);

            // re-create a new one
            this.editor = Editor.CreateEditor(node);
            this.Add(new IMGUIContainer(() =>
            {
                if(this.editor.target) { this.editor.OnInspectorGUI(); }
            }));
        }
    }
}
