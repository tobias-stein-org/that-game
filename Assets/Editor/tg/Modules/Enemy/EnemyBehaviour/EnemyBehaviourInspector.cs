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


            bool hasFlag(Enum value, EnemyBehaviour.BehaviourMask mask) { return ((EnemyBehaviour.BehaviourMask)value & mask) != 0; }

            var activeBehaviour = root.Q<EnumFlagsField>("activeBehaviour");
            {
                activeBehaviour.bindingPath = "activeBehaviour";
                activeBehaviour.RegisterValueChangedCallback((ChangeEvent<Enum> e) =>
                {
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Wander)   && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Wander)) { this.root.Q<PropertyField>("wander").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Avoid)    && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Avoid))  { this.root.Q<PropertyField>("avoid").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Flee)     && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Flee))   { this.root.Q<PropertyField>("flee").SetEnabled(true); }
                    if(!hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Pursue)   && hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Pursue)) { this.root.Q<PropertyField>("pursue").SetEnabled(true); }

                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Wander)    && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Wander)) { this.root.Q<PropertyField>("wander").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Avoid)     && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Avoid))  { this.root.Q<PropertyField>("avoid").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Flee)      && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Flee))   { this.root.Q<PropertyField>("flee").SetEnabled(false); }
                    if(hasFlag(e.previousValue, EnemyBehaviour.BehaviourMask.Pursue)    && !hasFlag(e.newValue, EnemyBehaviour.BehaviourMask.Pursue)) { this.root.Q<PropertyField>("pursue").SetEnabled(false); }
                });
            }

            this.root.Q<PropertyField>("wander").bindingPath    = "wander";
            this.root.Q<PropertyField>("avoid").bindingPath     = "avoid";
            this.root.Q<PropertyField>("flee").bindingPath      = "flee";
            this.root.Q<PropertyField>("pursue").bindingPath    = "pursue";

            this.root.Q<PropertyField>("wander").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Wander));
            this.root.Q<PropertyField>("avoid").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Avoid));
            this.root.Q<PropertyField>("flee").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Flee));
            this.root.Q<PropertyField>("pursue").SetEnabled(hasFlag(target.activeBehaviour, EnemyBehaviour.BehaviourMask.Pursue));

            return this.root;
        }
    }
}
