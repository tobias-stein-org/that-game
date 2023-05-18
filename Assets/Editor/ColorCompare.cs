using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class ColorCompare : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

    [MenuItem("MapGen/ColorCompare")]
    public static void ShowExample()
    {
        ColorCompare wnd = GetWindow<ColorCompare>();
        wnd.titleContent = new GUIContent("ColorCompare");
        wnd.minSize = wnd.maxSize = new Vector2(520, 260);
    }

    private Label de1;
    private Label de2;
    private Slider kL;
    private Slider kC;
    private Slider kH;
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

        this.de1 = root.Q<Label>("de1");
        this.de2 = root.Q<Label>("de2");
        this.kL = root.Q<Slider>("L");
        this.kC = root.Q<Slider>("C");
        this.kH = root.Q<Slider>("H");

        this.p1 = root.Q<ColorField>("c1picker");
        this.p2 = root.Q<ColorField>("c2picker");


        this.kL.RegisterValueChangedCallback((ChangeEvent<float> e) => { this.update(); });
        this.kC.RegisterValueChangedCallback((ChangeEvent<float> e) => { this.update(); });
        this.kH.RegisterValueChangedCallback((ChangeEvent<float> e) => { this.update(); });

        this.p1.RegisterValueChangedCallback((ChangeEvent<Color> e) => { c1.style.backgroundColor = e.newValue; this.update(); });
        this.p2.RegisterValueChangedCallback((ChangeEvent<Color> e) => { c2.style.backgroundColor = e.newValue; this.update(); });

        this.update();
    }

    private void update()
    {
        var lab1 = ColorComparer.RGBToLab(this.p1.value);
        var lab2 = ColorComparer.RGBToLab(this.p2.value);

        this.de1.text = $"{ColorComparer.DE00(lab1, lab2, this.kL.value, this.kC.value, this.kH.value)}";
        this.de2.text = $"{ColorComparer.DE00(lab2, lab1, this.kL.value, this.kC.value, this.kH.value)}";
    }
}
