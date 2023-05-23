using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace tg.editor.util
{
    using tg.util;

    public class ColorCompareEditorWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("tg/Utilities/Color Compare")]
        public static void ShowExample()
        {
            ColorCompareEditorWindow wnd = GetWindow<ColorCompareEditorWindow>();
            wnd.titleContent = new GUIContent("Color Compare");
            wnd.minSize = wnd.maxSize = new Vector2(410, 205);
        }

        private Label de1;
        private Label de2;
        private ColorField p1;
        private ColorField p2;

        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            var c1 = root.Q<VisualElement>("c1");
            var c2 = root.Q<VisualElement>("c2");

            this.p1 = root.Q<ColorField>("c1picker");
            this.p2 = root.Q<ColorField>("c2picker");
            this.de1 = root.Q<Label>("de1");
            this.de2 = root.Q<Label>("de2");

            this.p1.RegisterValueChangedCallback((ChangeEvent<Color> e) => { c1.style.backgroundColor = e.newValue; this.update(); });
            this.p2.RegisterValueChangedCallback((ChangeEvent<Color> e) => { c2.style.backgroundColor = e.newValue; this.update(); });

            this.update();
        }

        private void update()
        {

            this.de1.text = $"{this.p1.value.similarity(this.p2.value)}";
            this.de2.text = $"{this.p2.value.similarity(this.p1.value)}";
        }
    }
}
