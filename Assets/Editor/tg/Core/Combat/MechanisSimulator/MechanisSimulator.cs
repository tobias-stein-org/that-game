using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Random = System.Random;

namespace tg.editor.combat
{
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
            public int numCasts                 = 0;
            public int numDodges                = 0;
            public int numPhyCrit               = 0;
            public int numMagCrit               = 0;

            public float missRatio              = 0f;
            public float evasionRatio           = 0f;
            public float phyCritRatio           = 0f;
            public float magCritRatio           = 0f;

            public float phyDamageDealt         = 0f;
            public float magDamageDealt         = 0f;
            public float phyDamageTaken         = 0f;
            public float magDamageTaken         = 0f;

            public float phyDamageDealtAvg      = 0f;
            public float magDamageDealtAvg      = 0f;
            public float phyDamageTakenAvg      = 0f;
            public float magDamageTakenAvg      = 0f;

            public void clear()
            {
                this.numHits                    = 0;
                this.numCasts                   = 0;
                this.numDodges                  = 0;
                this.numPhyCrit                 = 0;
                this.numMagCrit                 = 0;

                this.missRatio                  = 0f;
                this.evasionRatio               = 0f;
                this.phyCritRatio               = 0f;
                this.magCritRatio               = 0f;

                this.phyDamageDealt             = 0f;
                this.magDamageDealt             = 0f;
                this.phyDamageTaken             = 0f;
                this.magDamageTaken             = 0f;

                this.phyDamageDealtAvg          = 0f;
                this.magDamageDealtAvg          = 0f;
                this.phyDamageTakenAvg          = 0f;
                this.magDamageTakenAvg          = 0f;
            }
        }

        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        [MenuItem("tg/Stats/Mechanis Simulator")]
        public static void ShowExample()
        {
            MechanisSimulator wnd               = GetWindow<MechanisSimulator>();
            wnd.titleContent                    = new GUIContent("Mechanis Simulator");
            wnd.minSize                         =
            wnd.maxSize                         = new Vector2(800, 600);
        }

        private int     iterations              = 300;
        private int     currentIteration        = 0;

        // attacker
        private Stats   attacker                = Stats.one;
        
        // defender
        private Stats   defender                = Stats.one;

        private Random  rng                     = new Random();
        
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
                var progress    = actions.Q<ProgressBar>("progress");
                var abort       = actions.Q<ToolbarButton>("abort");

                iterations.SetValueWithoutNotify(this.iterations);

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

                    run.SetEnabled(true);
                    iterations.SetEnabled(true);

                    EditorApplication.update   -= this.runSimulation;
                };

                iterations.RegisterValueChangedCallback((ChangeEvent<int> e) => { this.iterations = e.newValue; });
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

            // roll on evasion ...
            if(this.defenderChances.evasion > this.rng.NextDouble())
            {
                this.defenderResults.numDodges++;
            }
            else // attack hit.
            {
                this.attackerResults.numHits++;

                var phyDmg = this.attacker.physicalAttackRate(in this.defender) * (float)this.attacker.ATT;

                // roll on crit ...
                if(this.attackerChances.phyCritRate > this.rng.NextDouble())
                {
                    this.attackerResults.numPhyCrit++;
                    phyDmg *= 2.0f;
                }

                this.attackerResults.phyDamageDealt += phyDmg;
                this.defenderResults.phyDamageTaken += phyDmg;
            }

            // magic attacks can't miss
            this.attackerResults.numCasts++;

            var magDmg = this.attacker.magicalAttackRate(in this.defender) * (float)this.attacker.ATT;

            // roll on mag. crit ...
            if(this.attackerChances.magCritRate > this.rng.NextDouble())
            {
                this.attackerResults.numMagCrit++;
                magDmg *= 2.0f;
            }

            this.attackerResults.magDamageDealt += magDmg;
            this.defenderResults.magDamageTaken += magDmg;

            // udpate ratios

            this.attackerResults.missRatio          = (float)this.attackerResults.numHits      / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.phyCritRatio       = (float)this.attackerResults.numPhyCrit   / (float)Mathf.Max(1, this.attackerResults.numHits);
            this.attackerResults.magCritRatio       = (float)this.attackerResults.numMagCrit   / (float)Mathf.Max(1, this.attackerResults.numCasts);
            this.defenderResults.evasionRatio       = (float)this.defenderResults.numDodges    / (float)Mathf.Max(1, this.currentIteration);

            this.attackerResults.phyDamageDealtAvg  = this.attackerResults.phyDamageDealt / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.magDamageDealtAvg  = this.attackerResults.magDamageDealt / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.phyDamageTakenAvg  = this.attackerResults.phyDamageTaken / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.magDamageTakenAvg  = this.attackerResults.magDamageTaken / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.phyDamageDealtAvg  = this.defenderResults.phyDamageDealt / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.magDamageDealtAvg  = this.defenderResults.magDamageDealt / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.phyDamageTakenAvg  = this.defenderResults.phyDamageTaken / (float)Mathf.Max(1, this.currentIteration);
            this.defenderResults.magDamageTakenAvg  = this.defenderResults.magDamageTaken / (float)Mathf.Max(1, this.currentIteration);

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
            }

            var output                  = root.Q<Foldout>("output");
            {
                output.Q<Label>("no_output").style.display           = DisplayStyle.None;
                output.Q<VisualElement>("results").style.display     = DisplayStyle.Flex;
            }
        }
    }
}
