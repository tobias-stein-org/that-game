using System;
using System.Collections.Generic;

using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace tg.editor.enemy
{
    using tg.ai;
    using tg.ai.behaviour;
    using tg.enemy;

    [CustomEditor(typeof(EnemyBehaviour))]
    public class EnemyBehaviourInspector : Editor
    {
        [SerializeField]
        private VisualTreeAsset visualTreeAsset = default;

        private VisualElement   root = null;
        public override VisualElement CreateInspectorGUI()
        {
            var target  = this.target as EnemyBehaviour;
            this.root   = this.visualTreeAsset.Instantiate();

            #region perception

            var perception = this.root.Q<Foldout>("perception");

            perception.Q<SliderInt>("maxPerceptions").highValue = Sensor.Description.MAX_SENSOR_OUTPUTS;

            #endregion

            #region behaviours

            var behaviours = this.root.Q<Foldout>("behaviours");
            behaviours.Q<Slider>("frameBlending").bindingPath = "frameBlending";
            behaviours.Q<Slider>("contextBlurring").bindingPath = "contextBlurring";

            bool hasFlag(Enum value, EnemyBehaviour.BehaviourMask mask) { return ((EnemyBehaviour.BehaviourMask)value & mask) != 0; }

            var activeBehaviour = root.Q<EnumFlagsField>("activeBehaviour");
            {
                activeBehaviour.bindingPath = "activeBehaviour";
                activeBehaviour.RegisterValueChangedCallback((ChangeEvent<Enum> e) =>
                {
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Wander)   && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Wander)) { behaviours.Q<Foldout>("wander").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Avoid)    && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Avoid))  { behaviours.Q<Foldout>("avoid").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Flee)     && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Flee))   { behaviours.Q<Foldout>("flee").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Pursue)   && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Pursue)) { behaviours.Q<Foldout>("pursue").SetEnabled(true); }

                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Wander)    && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Wander)) { behaviours.Q<Foldout>("wander").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Avoid)     && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Avoid))  { behaviours.Q<Foldout>("avoid").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Flee)      && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Flee))   { behaviours.Q<Foldout>("flee").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Pursue)    && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Pursue)) { behaviours.Q<Foldout>("pursue").SetEnabled(false); }
                });
            }

            this.bindBehaviour("wander");
            this.bindBehaviour("avoid");
            this.bindBehaviour("flee");
            this.bindBehaviour("pursue");

            behaviours.Q<Foldout>("wander").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Wander));
            behaviours.Q<Foldout>("avoid").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Avoid));
            behaviours.Q<Foldout>("flee").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Flee));
            behaviours.Q<Foldout>("pursue").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Pursue));

            #endregion

            return this.root;
        }

        private void bindBehaviour(string name)
        {
            this.root.Q<PropertyField>(name).bindingPath                = name;
            this.root.Q<Foldout>(name).Q<Slider>("weight").bindingPath  = $"{name}Weight";
            this.root.Q<Foldout>(name).Q<Slider>("blend").bindingPath   = $"{name}Blend";
        }
    }
}
