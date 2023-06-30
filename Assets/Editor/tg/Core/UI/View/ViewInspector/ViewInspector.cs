using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace tg.editor.ui.view
{
    using tg.ui;

    [CustomEditor(typeof(View))]
    public class ViewInspector : Editor
    {
        [SerializeField]
        private VisualTreeAsset visualTreeAsset = default;

        private new View target { get { return base.target as View; } }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            var ui = this.visualTreeAsset.Instantiate();

            
            ui.Q<VisualElement>("actions").Q<Button>("editView").clicked += this.editView;
            ui.Q<VisualElement>("actions").Q<Button>("editController").clicked += this.editController;
            ui.Q<VisualElement>("actions").Q<Button>("delete").clicked += this.deleteView;



            root.Add(ui);
            root.Add(new IMGUIContainer(() => { DrawDefaultInspector(); }));

            return root;
        }

        private void editView()
        {
            AssetDatabase.OpenAsset(this.target.view);
        }

        private void editController()
        {
            Object controller = AssetDatabase.LoadAssetAtPath($"{ViewEditor.VIEW_DIR}/{this.target.name}/Controller.cs", typeof(Object));
            AssetDatabase.OpenAsset(controller);
        }

        private void deleteView()
        {
            if(EditorUtility.DisplayDialog("Delete View", $"Do your really want to delete the '{this.target.name}' view?", "Delete", "Abort"))
            {
                ViewEditor.deleteView(this.target);
            }
        }
    }
}
