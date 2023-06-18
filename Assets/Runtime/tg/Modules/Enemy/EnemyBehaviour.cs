using System;
using System.Collections.Generic;
using UnityEngine;

namespace tg.enemy
{
    using tg.ai;
    using tg.ai.behaviour;

    [CreateAssetMenu(menuName="tg/AI/Create Behaviour")]
    public class EnemyBehaviour : ScriptableObject
    {
        [Flags]
        public enum BehaviourMask
        {
            None    = 0,

            Wander  = 1,
            Avoid   = 1 << 1,
            Flee    = 1 << 2,
            Pursue  = 1 << 3,
        }

        public BehaviourMask    activeBehaviour;

        public Wander           wander;
        public Avoid            avoid;
        public Flee             flee;
        public Pursue           pursue;

        public static EnemyBehaviour Default
        {
            get
            {
                var behaviour = ScriptableObject.CreateInstance<EnemyBehaviour>();

                return behaviour;
            }
        }
    }
}