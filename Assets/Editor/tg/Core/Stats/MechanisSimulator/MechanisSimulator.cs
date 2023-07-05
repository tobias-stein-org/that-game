using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

using Random = System.Random;

namespace tg.editor.stats
{
    using tg.stats;

    public class MechanisSimulator : EditorWindow
    {
        [System.Serializable]
        public class Chances : ScriptableObject
        {
            public float phyHitAcc              = float.NaN;
            public float phyCritCh              = float.NaN;
            public float magCritCh              = float.NaN;
            public float phyCritRate            = float.NaN;
            public float magCritRate            = float.NaN;
            public float phyHitEva              = float.NaN;
            public float phyCritRes             = float.NaN;
            public float magCritRes             = float.NaN;
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
                var run         = actions.Q<ToolbarButton>("run");
                var iterations  = actions.Q<IntegerField>("iterations");
                var progress    = actions.Q<ProgressBar>("progress");
                var abort       = actions.Q<ToolbarButton>("abort");

                iterations.SetValueWithoutNotify(this.iterations);

                run.clicked    += () =>
                {
                    results.style.display       = DisplayStyle.None;
                    no_output.style.display     = DisplayStyle.Flex;
                    no_output.text              = "Simulation is running. Gathering output data...";

                    progress.SetValueWithoutNotify(0f);

                    progress.style.visibility   = Visibility.Visible;
                    abort.style.visibility      = Visibility.Visible;

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
            this.defenderChances = CreateInstance<Chances>();
            this.attackerResults = CreateInstance<Results>();
            this.defenderResults = CreateInstance<Results>();

            this.add(this.attackerChances, chances.Q<VisualElement>("att"));
            this.add(this.defenderChances, chances.Q<VisualElement>("def"));
            this.add(this.attackerResults, results.Q<VisualElement>("att"));
            this.add(this.defenderResults, results.Q<VisualElement>("def"));

            results.style.display = DisplayStyle.None;

            this.updateChances();
        }

        private void updateChances()
        {
            void update(Chances chances, in Stats stats)
            {
                chances.phyHitAcc   = this.attacker.physicalHitAccuracy(in stats);
                chances.phyCritCh   = this.attacker.physicalCriticalHitChance(in stats);
                chances.magCritCh   = this.attacker.magicalCriticalHitChance(in stats);
                chances.phyCritRate = this.attacker.physicalCriticalHitRate(in stats);
                chances.magCritRate = this.attacker.magicalCriticalHitRate(in stats);
                chances.phyHitEva   = this.defender.physicalHitEvasion(in stats);
                chances.phyCritRes  = this.defender.physicalCriticalHitResistance(in stats);
                chances.magCritRes  = this.defender.magicalCriticalHitResistance(in stats);
            }

            update(this.attackerChances, in this.defender);
            update(this.defenderChances, in this.attacker);
        }

        private void runSimulation()
        {
            if(this.currentIteration >= this.iterations)
            {
                this.simulationFinished();
                return;
            }

            // roll on evasion ...
            if(this.defenderChances.phyHitEva > this.rng.NextDouble())
            {
                this.defenderResults.numDodges++;
            }
            else // attack hit.
            {
                this.attackerResults.numHits++;

                // roll on crit ...
                if(this.attackerChances.phyCritRate > this.rng.NextDouble())
                {
                    this.attackerResults.numPhyCrit++;
                }
            }

            // magic attacks can't miss
            this.attackerResults.numCasts++;
            // roll on mag. crit ...
            if(this.attackerChances.magCritRate > this.rng.NextDouble())
            {
                this.attackerResults.numMagCrit++;
            }

            // udpate ratios

            this.attackerResults.missRatio      = (float)this.attackerResults.numHits      / (float)Mathf.Max(1, this.currentIteration);
            this.attackerResults.phyCritRatio   = (float)this.attackerResults.numPhyCrit   / (float)Mathf.Max(1, this.attackerResults.numHits);
            this.attackerResults.magCritRatio   = (float)this.attackerResults.numMagCrit   / (float)Mathf.Max(1, this.attackerResults.numCasts);
            this.defenderResults.evasionRatio   = (float)this.defenderResults.numDodges    / (float)Mathf.Max(1, this.currentIteration);

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
