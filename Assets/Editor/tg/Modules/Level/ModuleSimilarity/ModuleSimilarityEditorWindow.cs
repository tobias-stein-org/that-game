using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Linq;

using static tg.level.generator.step.RenderMazeStep.ModuleConstraints;

namespace tg.editor.level
{
    using tg.level;
    using tg.util;

    public class ModuleSimilarityEditorWindow : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("tg/Level/Compare Modules")]
        public static void OpenWindow()
        {
            var wnd = GetWindow<ModuleSimilarityEditorWindow>();
            wnd.titleContent = new GUIContent("Compare Modules");
            wnd.minSize = new Vector2(710, 710);
            wnd.maxSize = new Vector2(710, 710);
        }


        private enum TextureID
        {
            ModuleA = 0,
            ModuleB,

            ModuleA_Left,   ModuleB_Left,
            ModuleA_Right,  ModuleB_Right,
            ModuleA_Top,    ModuleB_Top,
            ModuleA_Bottom, ModuleB_Bottom,

            Similar_Left,
            Similar_Right,
            Similar_Top,
            Similar_Bottom,

            MaxTextures
        };

        private enum SimScoreFunction
        {
            Avg,
            Med,
            Sum,
            Min,
            Max,
            LMM,
            PCT
        };

        private Module moduleA, moduleB;
        private Texture2D[] textures = new Texture2D[(int)TextureID.MaxTextures];

        private float[][] similarVals = new float[4][];


        public void CreateGUI()
        {
            // Each editor window contains a root VisualElement object
            VisualElement root = rootVisualElement;

            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            root.Add(labelFromUXML);

            root.Q<ObjectField>("moduleA").objectType = typeof(Module);
            root.Q<ObjectField>("moduleA").RegisterValueChangedCallback((ChangeEvent<Object> e) =>
            {
                this.moduleA = e.newValue as Module;
                this.updatePreviewTextures();
            });

            root.Q<ObjectField>("moduleB").objectType = typeof(Module);
            root.Q<ObjectField>("moduleB").RegisterValueChangedCallback((ChangeEvent<Object> e) =>
            {
                this.moduleB = e.newValue as Module;
                this.updatePreviewTextures();
            });


            root.Q<EnumField>("simFunc").RegisterValueChangedCallback((ChangeEvent<System.Enum> e) =>
            {
                this.rootVisualElement.Q<Slider>("percentile").visible = (SimScoreFunction)e.newValue == SimScoreFunction.PCT;
            });

            var threshold = root.Q<Slider>("threshold");
            var indicator = root.Q<GradientField>("indicator");
            indicator.pickingMode = PickingMode.Ignore;

            // Create a new gradient
            Gradient gradient = new Gradient();
        
            // Define colors and times for the gradient
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(Color.green, 0f);
            colorKeys[1] = new GradientColorKey(Color.red, threshold.value / threshold.highValue);
        
            GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
            alphaKeys[0] = new GradientAlphaKey(1f, 0f);
            alphaKeys[1] = new GradientAlphaKey(1f, 1f);
        
            gradient.SetKeys(colorKeys, alphaKeys);


            gradient.mode = GradientMode.PerceptualBlend;
            indicator.value = gradient;


            threshold.RegisterValueChangedCallback((ChangeEvent<float> e) =>
            {
                // Create a new gradient
                Gradient gradient = new Gradient();
        
                // Define colors and times for the gradient
                GradientColorKey[] colorKeys = new GradientColorKey[2];
                colorKeys[0] = new GradientColorKey(Color.green, 0f);
                colorKeys[1] = new GradientColorKey(Color.red, e.newValue / threshold.highValue);
        
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
        
                gradient.SetKeys(colorKeys, alphaKeys);


                gradient.mode = GradientMode.PerceptualBlend;
                indicator.value = gradient;

                this.updatePreviewTextures();
            });

            this.rootVisualElement.Q<SliderInt>("blurSize").RegisterValueChangedCallback((ChangeEvent<int> e) => this.updatePreviewTextures());
            this.rootVisualElement.Q<Slider>("blurStrength").RegisterValueChangedCallback((ChangeEvent<float> e) => this.updatePreviewTextures());
        }

        [MenuItem("Assets/tg/Level/Compare Modules", true)]
        static public bool OnContext_Validate()
        {
            if(Selection.objects.Length != 2) { return false; }

            return Selection.objects.All(o => o.GetType() == typeof(Module));
        }

        [MenuItem("Assets/tg/Level/Compare Modules")]
        static public void OnContext()
        {
            ModuleSimilarityEditorWindow wnd = GetWindow<ModuleSimilarityEditorWindow>();
            wnd.minSize = new Vector2(710, 710);
            wnd.maxSize = new Vector2(710, 710);

            wnd.moduleA = Selection.objects[0] as Module;
            wnd.moduleB = Selection.objects[1] as Module;

            wnd.updatePreviewTextures();

            wnd.rootVisualElement.Q<ObjectField>("moduleA").SetValueWithoutNotify(wnd.moduleA);
            wnd.rootVisualElement.Q<ObjectField>("moduleB").SetValueWithoutNotify(wnd.moduleB);
        }

        private void updatePreviewTextures()
        {
            foreach(var t in this.textures) { Texture2D.DestroyImmediate(t); }

            if(this.moduleA)
            {
                var texture                                     = this.getTexture(this.moduleA);
                this.textures[(int)TextureID.ModuleA]           = texture;
                this.textures[(int)TextureID.ModuleA_Left]      = this.getBorder(texture, Side.Left);
                this.textures[(int)TextureID.ModuleA_Right]     = this.getBorder(texture, Side.Right);
                this.textures[(int)TextureID.ModuleA_Top]       = this.getBorder(texture, Side.Top);
                this.textures[(int)TextureID.ModuleA_Bottom]    = this.getBorder(texture, Side.Bottom);
            }

            if(this.moduleB)
            {
                var texture                                     = this.getTexture(this.moduleB);
                this.textures[(int)TextureID.ModuleB]           = texture;
                this.textures[(int)TextureID.ModuleB_Left]      = this.getBorder(texture, Side.Left);
                this.textures[(int)TextureID.ModuleB_Right]     = this.getBorder(texture, Side.Right);
                this.textures[(int)TextureID.ModuleB_Top]       = this.getBorder(texture, Side.Top);
                this.textures[(int)TextureID.ModuleB_Bottom]    = this.getBorder(texture, Side.Bottom);
            }

            if(this.moduleA && this.moduleB)
            {
                this.textures[(int)TextureID.Similar_Left]      = this.getSimilar(this.textures[(int)TextureID.ModuleA_Left],   this.textures[(int)TextureID.ModuleB_Right], Side.Left);
                this.textures[(int)TextureID.Similar_Right]     = this.getSimilar(this.textures[(int)TextureID.ModuleA_Right],  this.textures[(int)TextureID.ModuleB_Left], Side.Right);
                this.textures[(int)TextureID.Similar_Top]       = this.getSimilar(this.textures[(int)TextureID.ModuleA_Top],    this.textures[(int)TextureID.ModuleB_Bottom], Side.Top);
                this.textures[(int)TextureID.Similar_Bottom]    = this.getSimilar(this.textures[(int)TextureID.ModuleA_Bottom], this.textures[(int)TextureID.ModuleB_Top], Side.Bottom);
            }
        }


        private float percentile(float[] values, float percentile)
        {
            // Sort the array of values in ascending order
            float[] sortedValues = values.OrderBy(x => x).ToArray();

            // Calculate the index corresponding to the desired percentile
            float index = (percentile / 100.0f) * (sortedValues.Length - 1);

            // Separate the whole and fractional parts of the index
            int lowerIndex = Mathf.FloorToInt(index);
            float fractionalPart = index - lowerIndex;

            // Interpolate the percentile value
            float lowerValue = sortedValues[lowerIndex];
            float upperValue = sortedValues[lowerIndex + 1];
            return lowerValue + (upperValue - lowerValue) * fractionalPart;

        }

        private float computeSimScore(Side side)
        {
            var values = this.similarVals[(int)side];

            switch((SimScoreFunction)(this.rootVisualElement.Q<EnumField>("simFunc").value))
            {
                case SimScoreFunction.Min: return values.Min();
                case SimScoreFunction.Max: return values.Max();
                case SimScoreFunction.Sum: return values.Sum();
                case SimScoreFunction.Avg: return values.Average();
                case SimScoreFunction.Med: return percentile(values, 50f);
                case SimScoreFunction.LMM: return (values[0] + values[Mathf.FloorToInt(values.Length / 2)] + values[values.Length - 1]) / 3.0f;
                case SimScoreFunction.PCT: return percentile(values, this.rootVisualElement.Q<Slider>("percentile").value);

            }

            return float.NaN;
        }

        void OnGUI()
        {
            var previewRect             = this.rootVisualElement.Q<VisualElement>("root").contentRect;

            int margin                  = 12;
            int borderTextureSize       = 5;
            int borderSpacing           = 3 * margin + 2 * borderTextureSize;
            int extraHeight             = 2 * borderSpacing;

            int modulePreviewSize       = Mathf.FloorToInt(Mathf.Min(previewRect.width, previewRect.height - extraHeight) / 3.0f) - 20;
            int modulePreviewSizeHalf   = Mathf.FloorToInt(modulePreviewSize / 2.0f);

            previewRect.position        = new Vector2(0, -20);


            var threshold = this.rootVisualElement.Q<Slider>("threshold");
        
            EditorGUI.LabelField(new Rect(125 + (threshold.contentRect.width - 125) * (threshold.value / threshold.highValue), previewRect.height - 50, 100, 20), $"{threshold.value}");
            if(this.moduleA)
            {
                EditorGUI.DrawPreviewTexture(
                    new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - modulePreviewSizeHalf,
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   this.textures[(int)TextureID.ModuleA]
                );

                EditorGUI.LabelField(new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - ((new GUIStyle(GUI.skin.label)).CalcSize(new GUIContent("Module A")).x * 0.5f),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   "Module A");

                // cut-outs
                {
                    // TOP
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf + margin + borderTextureSize),
                            modulePreviewSize, borderTextureSize
                       ),
                       this.textures[(int)TextureID.ModuleA_Top]
                    );

                    // BOTTOM
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (modulePreviewSizeHalf + margin),
                            modulePreviewSize, borderTextureSize
                       ),
                       this.textures[(int)TextureID.ModuleA_Bottom]
                    );

                    // LEFT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf + margin + borderTextureSize),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            borderTextureSize, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.ModuleA_Left]
                    );

                    // RIGHT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) + (modulePreviewSizeHalf + margin),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            borderTextureSize, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.ModuleA_Right]
                    );
                }
            }

            if(this.moduleB)
            {
                // TOP
                EditorGUI.DrawPreviewTexture(
                    new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - (modulePreviewSizeHalf),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (3.0f * modulePreviewSizeHalf + borderSpacing),
                        modulePreviewSize, modulePreviewSize
                   ),
                   this.textures[(int)TextureID.ModuleB]
                );


                var lT = "Module B";
                var lB = "Module B";
                var lL = "Module B";
                var lR = "Module B";
                var sT = 0.0f;
                var sB = 0.0f;
                var sL = 0.0f;
                var sR = 0.0f;
                //var threshold = this.rootVisualElement.Q<Slider>("threshold").value;
            
                if(this.moduleA)
                {
                    sT = this.computeSimScore(Side.Top);
                    sB = this.computeSimScore(Side.Bottom);
                    sL = this.computeSimScore(Side.Left);
                    sR = this.computeSimScore(Side.Right);

                    lT = $"Module B [{sT.ToString("0.000")}]";
                    lB = $"Module B [{sB.ToString("0.000")}]";
                    lL = $"Module B [{sL.ToString("0.000")}]";
                    lR = $"Module B [{sR.ToString("0.000")}]";
                }

                EditorGUI.LabelField(new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - ((new GUIStyle(GUI.skin.label)).CalcSize(new GUIContent(lT)).x * 0.5f),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (3.0f * modulePreviewSizeHalf + borderSpacing),
                        modulePreviewSize, modulePreviewSize
                   ),
                   lT);

                // BOTTOM
                EditorGUI.DrawPreviewTexture(
                    new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - modulePreviewSizeHalf,
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (1.0f * modulePreviewSizeHalf + borderSpacing),
                        modulePreviewSize, modulePreviewSize
                   ),
                   this.textures[(int)TextureID.ModuleB]
                );

                EditorGUI.LabelField(new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - ((new GUIStyle(GUI.skin.label)).CalcSize(new GUIContent(lB)).x * 0.5f),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (1.0f * modulePreviewSizeHalf + borderSpacing),
                        modulePreviewSize, modulePreviewSize
                   ),
                   lB);

                // LEFT
                EditorGUI.DrawPreviewTexture(
                    new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - (3.0f * modulePreviewSizeHalf + borderSpacing),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   this.textures[(int)TextureID.ModuleB]
                );

                EditorGUI.LabelField(new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - (2.0f * modulePreviewSizeHalf + borderSpacing) - ((new GUIStyle(GUI.skin.label)).CalcSize(new GUIContent(lL)).x * 0.5f),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   lL);

                // RIGHT
                EditorGUI.DrawPreviewTexture(
                    new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) + (1.0f * modulePreviewSizeHalf + borderSpacing),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   this.textures[(int)TextureID.ModuleB]
                );

                EditorGUI.LabelField(new Rect(
                        previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) + (2.0f * modulePreviewSizeHalf + borderSpacing) - ((new GUIStyle(GUI.skin.label)).CalcSize(new GUIContent(lR)).x * 0.5f),
                        previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf,
                        modulePreviewSize, modulePreviewSize
                   ),
                   lR);

                 // Set the color to red
                Handles.color = Color.red;


                if(sT >= threshold.value)
                    {
                        // Draw a line
            Vector3 startPoint = new Vector3(previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - (modulePreviewSizeHalf), previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (3.0f * modulePreviewSizeHalf + borderSpacing), 0);
            Vector3 endPoint = startPoint + new Vector3(modulePreviewSize, modulePreviewSize, 0);
            Handles.DrawAAPolyLine(6f, startPoint, endPoint);
            Handles.DrawAAPolyLine(6f, startPoint + new Vector3(modulePreviewSize, 0), endPoint - new Vector3(modulePreviewSize, 0));
                    }

                    if(sB >= threshold.value)
                    {
                    // Draw a line
            Vector3 startPoint = new Vector3(previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - modulePreviewSizeHalf, previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (1.0f * modulePreviewSizeHalf + borderSpacing), 0);
            Vector3 endPoint = startPoint + new Vector3(modulePreviewSize, modulePreviewSize, 0);
            Handles.DrawAAPolyLine(6f, startPoint, endPoint);
            Handles.DrawAAPolyLine(6f, startPoint + new Vector3(modulePreviewSize, 0), endPoint - new Vector3(modulePreviewSize, 0));
                    }

                    if(sL >= threshold.value)
                    {
       
            Vector3 startPoint = new Vector3(previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) - (3.0f * modulePreviewSizeHalf + borderSpacing), previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf, 0);
            Vector3 endPoint = startPoint + new Vector3(modulePreviewSize, modulePreviewSize, 0);
            Handles.DrawAAPolyLine(6f, startPoint, endPoint);
            Handles.DrawAAPolyLine(6f, startPoint + new Vector3(modulePreviewSize, 0), endPoint - new Vector3(modulePreviewSize, 0));
                    }

                    if(sR >= threshold.value)
                    {
                  
            Vector3 startPoint = new Vector3(previewRect.x + Mathf.FloorToInt(previewRect.width * 0.5f) + (1.0f * modulePreviewSizeHalf + borderSpacing), previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - modulePreviewSizeHalf, 0);
            Vector3 endPoint = startPoint + new Vector3(modulePreviewSize, modulePreviewSize, 0);
            Handles.DrawAAPolyLine(6f, startPoint, endPoint);
            Handles.DrawAAPolyLine(6f, startPoint + new Vector3(modulePreviewSize, 0), endPoint - new Vector3(modulePreviewSize, 0));
                    }

                // cut-outs
                { 
                    // TOP
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf + 2*(margin + borderTextureSize)),
                            modulePreviewSize, borderTextureSize
                       ),
                       this.textures[(int)TextureID.ModuleB_Bottom]
                    );

                    // BOTTOM
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (modulePreviewSizeHalf + 2*margin + borderTextureSize),
                            modulePreviewSize, borderTextureSize
                       ),
                       this.textures[(int)TextureID.ModuleB_Top]
                    );

                    // LEFT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf + 2*(margin + borderTextureSize)),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            borderTextureSize, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.ModuleB_Right]
                    );

                    // RIGHT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) + (modulePreviewSizeHalf + 2*margin + borderTextureSize),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            borderTextureSize, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.ModuleB_Left]
                    );
                }
            }

            if(this.moduleA && this.moduleB)
            {
                // similarities
                {
                    // TOP
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf + 2*margin + borderTextureSize),
                            modulePreviewSize, margin
                       ),
                       this.textures[(int)TextureID.Similar_Top]
                    );

                    // BOTTOM
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) + (modulePreviewSizeHalf + margin + borderTextureSize),
                            modulePreviewSize, margin - 2
                       ),
                       this.textures[(int)TextureID.Similar_Bottom]
                    );

                    // LEFT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) - (modulePreviewSizeHalf + 2*margin + borderTextureSize),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            margin, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.Similar_Left]
                    );

                    // RIGHT
                    EditorGUI.DrawPreviewTexture(
                        new Rect(
                            previewRect.x + Mathf.FloorToInt(previewRect.width  * 0.5f) + (modulePreviewSizeHalf + margin + borderTextureSize),
                            previewRect.y + Mathf.FloorToInt(previewRect.height * 0.5f) - (modulePreviewSizeHalf),
                            margin, modulePreviewSize
                       ),
                       this.textures[(int)TextureID.Similar_Right]
                    );
                }
            }
        }

        private Texture2D getTexture(Module module)
        {
            var rect = module.sprite.textureRect;

            var pixels = module.sprite.texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height, 0);

            var texture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);

            //texture.SetPixels(pixels);
            //texture.SetPixels(pixels.Select(pixel => Color.Lerp(Color.clear, pixel, pixel.a)).ToArray());
            texture.SetPixels(pixels.Select(pixel => pixel.a < 1.0f ? Color.magenta : pixel).ToArray());
            texture.filterMode = FilterMode.Point;

            texture.Apply();

            return texture;
        }

        private Texture2D getBorder(Texture2D a, Side side)
        {
            var X = a.width;
            var Y = a.height;
            var P = a.GetPixels();

            Color[] pixels = null;

            switch(side)
            {
                case Side.Left:
                {
                    pixels = new Color[Y];
                    for(int py = 0; py < Y; py++)
                    {
                        pixels[py] = P[py * X];
                    }
                    X = 1;
                    break;
                }

                case Side.Right:
                {
                    pixels = new Color[Y];
                    var rightBorderOffset   = (X - 1);

                    for(int py = 0; py < Y; py++)
                    {
                        pixels[py] = P[py * X + rightBorderOffset];

                    }
                    X = 1;
                    break;
                }

                case Side.Bottom:
                {
                    pixels = new Color[X];
                    for(int px = 0; px < X; px++)
                    {
                        pixels[px] = P[px];                       
                    }
                    Y = 1;
                    break;
                }

                case Side.Top:
                {
                    pixels = new Color[X];
                    var bottomBorderOffset  = (Y - 1) * X;

                    for(int px = 0; px < X; px++)
                    {
                        pixels[px] = P[px + bottomBorderOffset];  
                    }
                    Y = 1;
                    break;
                }
            }

            var t = new Texture2D(X, Y, TextureFormat.RGBA32, false);
            t.SetPixels(
                this.applyBlur1D(pixels, this.rootVisualElement.Q<SliderInt>("blurSize").value, this.rootVisualElement.Q<Slider>("blurStrength").value)
            );
            //t.SetPixels(pixels);
        
            t.filterMode = FilterMode.Point;
            t.Apply();

            return t;
        }

        private Texture2D getSimilar(Texture2D a, Texture2D b, Side side)
        {
            Texture2D t = new Texture2D(a.width, a.height, a.format, false);

            Color[] pixel   = new Color[a.width * a.height];
        
            var pa = a.GetPixels();
            var pb = b.GetPixels();
            var pc = new Color[pa.Length];

            this.similarVals[(int)side] = new float[pa.Length];

            var threshold = this.rootVisualElement.Q<Slider>("threshold").value;
            for(int i = 0; i < pa.Length; i++)
            {
                var alphaAdj = pa[i].a < 1.0f ? Color.Lerp(pb[i], pa[i], pa[i].a) : pa[i];

                var sim = Mathf.Clamp(alphaAdj.similarity(pb[i]), 0.0f, threshold);
                this.similarVals[(int)side][i] = sim;
                pc[i] = Color.LerpUnclamped(Color.green, Color.red, sim / threshold);
            }

            t.SetPixels(pc);
            t.filterMode = FilterMode.Point;
            t.Apply();

            return t;
        }

        Color[] applyBlur1D(Color[] pixels, int blurSize, float blurStrength)
        {
            Color[] blurredPixels = new Color[pixels.Length];

            for (int x = 0; x < pixels.Length; x++)
            {
                Color accumulatedColor = Color.black;
                float totalWeight = 0f;

                for (int i = -blurSize; i <= blurSize; i++)
                {
                    int offsetX = Mathf.Clamp(x + i, 0, pixels.Length - 1);
                    Color pixel = pixels[offsetX];

                    float weight = Mathf.Exp(-i * i / (2f * blurStrength * blurStrength));
                    accumulatedColor += pixel * weight;
                    totalWeight += weight;
                }

                blurredPixels[x] = accumulatedColor / totalWeight;
            }

            return blurredPixels;
        }
    }
}

