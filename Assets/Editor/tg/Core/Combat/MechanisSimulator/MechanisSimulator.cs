using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Random = System.Random;

namespace tg.editor.combat
{
    using tg.combat;
    using tg.combat.entities;

    public class MechanisSimulator : EditorWindow
    {
        [System.Serializable]
        public class Chances : ScriptableObject
        {
            [Range(0,1)]
            public float accuracy               = float.NaN;
            [Range(0, 1)]
            public float evasion                = float.NaN;
            [Range(0, 1)]
            public float advantage              = float.NaN;

            [Range(0, 1)]
            public float phyCritCh              = float.NaN;
            [Range(0,1)]
            public float magCritCh              = float.NaN;
            [Range(0,1)]
            public float phyRes                 = float.NaN;
            [Range(0,1)]
            public float magRes                 = float.NaN;
            [Range(0,1)]
            public float phyCritRate            = float.NaN;
            [Range(0,1)]
            public float magCritRate            = float.NaN;

            [Range(0,1)]
            public float phyDamRate             = float.NaN;
            [Range(0,1)]
            public float magDamRate             = float.NaN;
        }

        [System.Serializable]
        public class Results : ScriptableObject
        {
            public int numHits                  = 0;
            public int numDodges                = 0;
            public int numCrits                 = 0;

            public float missRatio              = 0f;
            public float evasionRatio           = 0f;
            public float critRatio              = 0f;

            public float damageDealt            = 0f;
            public float damageTaken            = 0f;

            public float damageDealtAvg         = 0f;
            public float damageTakenAvg         = 0f;

            public void clear()
            {
                this.numHits                    = 0;
                this.numDodges                  = 0;
                this.numCrits                   = 0;

                this.missRatio                  = 0f;
                this.evasionRatio               = 0f;
                this.critRatio                  = 0f;

                this.damageDealt                = 0f;
                this.damageTaken                = 0f;

                this.damageDealtAvg             = 0f;
                this.damageTakenAvg             = 0f;
            }
        }

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("tg/Stats/Mechanis Simulator")]
        public static void ShowExample()
        {
            MechanisSimulator wnd                   = GetWindow<MechanisSimulator>();
            wnd.titleContent                        = new GUIContent("Mechanis Simulator");
            wnd.minSize                             =
            wnd.maxSize                             = new Vector2(800, 600);
        }

        private int         iterations              = 300;
        private int         currentIteration        = 0;

        // attacker
        private Stats       attacker                = Stats.one;
        
        // defender
        private Stats       defender                = Stats.one;

        private Damage.Type damageType              = Damage.Type.Weapon_Melee;

        private Chances attackerChances, defenderChances;
        private Results attackerResults, defenderResults;

        private void add<T>(T scriptableObject, VisualElement root, bool readOnly = true)
            where T : ScriptableObject
        {
            var so = new SerializedObject(scriptableObject);
            foreach(var field in scriptableObject.GetType().GetFields())
            {
                var f = new PropertyField();
                f.label = field.Name;
                f.SetEnabled(!readOnly);
                f.BindProperty(so.FindProperty(field.Name));
                root.Add(f);
            }
        }

        public void CreateGUI()
        {
            this.rootVisualElement.Add(m_VisualTreeAsset.Instantiate());

            var root            = this.rootVisualElement;

            var no_output       = root.Q<Foldout>("output").Q<Label>("no_output");
            var results         = root.Q<Foldout>("output").Q<VisualElement>("results");

            var actions         = root.Q<Toolbar>("actions");
            {
                var reset       = actions.Q<ToolbarButton>("reset");
                var run         = actions.Q<ToolbarButton>("run");
                var iterations  = actions.Q<IntegerField>("iterations");
                var damageType  = actions.Q<EnumField>("damageType");
                var progress    = actions.Q<ProgressBar>("progress");
                var abort       = actions.Q<ToolbarButton>("abort");

                iterations.SetValueWithoutNotify(this.iterations);
                damageType.SetValueWithoutNotify(this.damageType);

                reset.clicked += () =>
                {
                    this.attacker = this.defender = Stats.one;

                    var stats = root.Q<Foldout>("stats");
                    {
                        var att = stats.Q<VisualElement>("attacker");
                        {
                            att.Q<SliderInt>("str").SetValueWithoutNotify((int)this.attacker.STR);
                            att.Q<SliderInt>("agi").SetValueWithoutNotify((int)this.attacker.AGI);
                            att.Q<SliderInt>("int").SetValueWithoutNotify((int)this.attacker.INT);
                            att.Q<SliderInt>("att").SetValueWithoutNotify((int)this.attacker.ATT);
                            att.Q<SliderInt>("def").SetValueWithoutNotify((int)this.attacker.DEF);
                        }

                        var def = stats.Q<VisualElement>("defender");
                        {
                            def.Q<SliderInt>("str").SetValueWithoutNotify((int)this.defender.STR);
                            def.Q<SliderInt>("agi").SetValueWithoutNotify((int)this.defender.AGI);
                            def.Q<SliderInt>("int").SetValueWithoutNotify((int)this.defender.INT);
                            def.Q<SliderInt>("att").SetValueWithoutNotify((int)this.defender.ATT);
                            def.Q<SliderInt>("def").SetValueWithoutNotify((int)this.defender.DEF);
                        }
                    }
                    this.updateChances();
                };

                run.clicked    += () =>
                {
                    results.style.display       = DisplayStyle.None;
                    no_output.style.display     = DisplayStyle.Flex;
                    no_output.text              = "Simulation is running. Gathering output data...";

                    progress.SetValueWithoutNotify(0f);

                    progress.style.visibility   = Visibility.Visible;
                    abort.style.visibility      = Visibility.Visible;

                    reset.SetEnabled(false);
                    run.SetEnabled(false);
                    iterations.SetEnabled(false);
                    damageType.SetEnabled(false);

                    this.currentIteration       = 0;
                    this.attackerResults.clear();
                    this.defenderResults.clear();

                    EditorApplication.update   += this.runSimulation;
                };

                abort.clicked += () =>
                {
                    no_output.style.display     = DisplayStyle.Flex;
                    no_output.text              = "Simulation aborted. No output data available.";

                    progress.style.visibility   = Visibility.Hidden;
                    abort.style.visibility      = Visibility.Hidden;

                    reset.SetEnabled(true);
                    run.SetEnabled(true);
                    iterations.SetEnabled(true);
                    damageType.SetEnabled(true);

                    EditorApplication.update   -= this.runSimulation;
                };

                iterations.RegisterValueChangedCallback((ChangeEvent<int> e) => { this.iterations = e.newValue; });
                damageType.RegisterValueChangedCallback((ChangeEvent<System.Enum> e) => { this.damageType = (Damage.Type)e.newValue; });
            }

            var chances         = root.Q<Foldout>("chances");
            var stats           = root.Q<Foldout>("stats");
            {
                var att         = stats.Q<VisualElement>("attacker");
                {
                    att.Q<SliderInt>("str").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.attacker.STR = (uint)e.newValue; this.updateChances(); });
                    att.Q<SliderInt>("agi").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.attacker.AGI = (uint)e.newValue; this.updateChances(); });
                    att.Q<SliderInt>("int").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.attacker.INT = (uint)e.newValue; this.updateChances(); });
                    att.Q<SliderInt>("att").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.attacker.ATT = (uint)e.newValue; this.updateChances(); });
                    att.Q<SliderInt>("def").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.attacker.DEF = (uint)e.newValue; this.updateChances(); });
                }

                var def         = stats.Q<VisualElement>("defender");
                {
                    def.Q<SliderInt>("str").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.defender.STR = (uint)e.newValue; this.updateChances(); });
                    def.Q<SliderInt>("agi").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.defender.AGI = (uint)e.newValue; this.updateChances(); });
                    def.Q<SliderInt>("int").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.defender.INT = (uint)e.newValue; this.updateChances(); });
                    def.Q<SliderInt>("att").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.defender.ATT = (uint)e.newValue; this.updateChances(); });
                    def.Q<SliderInt>("def").RegisterValueChangedCallback((ChangeEvent<int> e) => { this.defender.DEF = (uint)e.newValue; this.updateChances(); });
                }
            }

            this.attackerChances = CreateInstance<Chances>();
            this.attackerResults = CreateInstance<Results>();
            this.defenderChances = CreateInstance<Chances>();
            this.defenderResults = CreateInstance<Results>();

            this.add(this.attackerChances, chances.Q<VisualElement>("att"));
            this.add(this.attackerResults, results.Q<VisualElement>("att"));
            this.add(this.defenderChances, chances.Q<VisualElement>("def"));
            this.add(this.defenderResults, results.Q<VisualElement>("def"));

            results.style.display = DisplayStyle.None;

            this.updateChances();
        }

        private void updateChances()
        {
            void update(Chances chances, in Stats stats0, in Stats stats1)
            {
                chances.accuracy    = stats0.accuracy(in stats1);
                chances.evasion     = stats0.evasion(in stats1);

                chances.advantage   = stats0.advantage(in stats1);

                chances.phyCritCh   = stats0.physicalCriticalHitChance(in stats1);
                chances.magCritCh   = stats0.magicalCriticalHitChance(in stats1);
                chances.phyCritRate = stats0.physicalCriticalHitRate(in stats1);
                chances.magCritRate = stats0.magicalCriticalHitRate(in stats1);
                chances.phyRes      = stats0.physicalResistance(in stats1);
                chances.magRes      = stats0.magicalResistance(in stats1);

                chances.phyDamRate  = stats0.physicalAttackRate(in stats1);
                chances.magDamRate  = stats0.magicalAttackRate(in stats1);
            }

            update(this.attackerChances, in this.attacker, in this.defender);
            update(this.defenderChances, in this.defender, in this.attacker);
        }

        private void runSimulation()
        {
            if(this.currentIteration >= this.iterations)
            {
                this.simulationFinished();
                return;
            }

            var result                          = new Damage
            {
                type                            = this.damageType,
                value                           = this.attacker.ATT
            }.roll(this.attacker, this.defender);

            this.defenderResults.numDodges      += result.missed    ? 1 : 0;
            this.attackerResults.numHits        += result.missed    ? 0 : 1;
            this.attackerResults.numCrits       += result.critical  ? 1 : 0;

            this.attackerResults.damageDealt    += result.value;
            this.defenderResults.damageTaken    += result.value;

            // udpate ratios

            this.attackerResults.missRatio       = Mathf.Clamp01(1.0f - ((float)this.attackerResults.numHits    / (float)Mathf.Max(1, this.currentIteration)));
            this.attackerResults.critRatio       = Mathf.Clamp01((float)this.attackerResults.numCrits   / (float)Mathf.Max(1, this.attackerResults.numHits));
            this.defenderResults.evasionRatio    = Mathf.Clamp01((float)this.defenderResults.numDodges  / (float)Mathf.Max(1, this.currentIteration));

            this.attackerResults.damageDealtAvg  = this.attackerResults.damageDealt       / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.damageTakenAvg  = this.attackerResults.damageTaken       / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.damageDealtAvg  = this.defenderResults.damageDealt       / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.damageTakenAvg  = this.defenderResults.damageTaken       / (float)Mathf.Max(1, this.currentIteration);

            this.rootVisualElement.Q<Toolbar>("actions").Q<ProgressBar>("progress").value = (float)this.currentIteration / (float)this.iterations;
            this.currentIteration++;
        }

        private void OnDisable()
        {
            EditorApplication.update   -= this.runSimulation;
        }

        private void simulationFinished()
        {
            EditorApplication.update   -= this.runSimulation;

            var root                    = this.rootVisualElement;
            var actions                 = root.Q<Toolbar>("actions");
            {
                actions.Q<ProgressBar>("progress").style.visibility = Visibility.Hidden;
                actions.Q<ToolbarButton>("abort").style.visibility  = Visibility.Hidden;

                actions.Q<ToolbarButton>("reset").SetEnabled(true);
                actions.Q<ToolbarButton>("run").SetEnabled(true);
                actions.Q<IntegerField>("iterations").SetEnabled(true);
                actions.Q<EnumField>("damageType").SetEnabled(true);
            }

            var output                  = root.Q<Foldout>("output");
            {
                output.Q<Label>("no_output").style.display           = DisplayStyle.None;
                output.Q<VisualElement>("results").style.display     = DisplayStyle.Flex;
            }
        }
    }
}
