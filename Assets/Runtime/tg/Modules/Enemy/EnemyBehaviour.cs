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
        #region Behaviours

        [Flags]
        public enum BehaviourMask
        {
            None    = 0,

            Wander  = 1,
            Avoid   = 1 << 1,
            Flee    = 1 << 2,
            Pursue  = 1 << 3,
        }

        public BehaviourMask    activeBehaviour = BehaviourMask.None;

        public Wander           wander          = Wander.Default;
        public float            wanderWeight    = 1.0f;
        public float            wanderBlend     = 0.0f;

        public Avoid            avoid           = Avoid.Default;
        public float            avoidWeight     = 1.0f;
        public float            avoidBlend      = 0.0f;

        public Flee             flee            = Flee.Default;
        public float            fleeWeight      = 1.0f;
        public float            fleeBlend       = 0.0f;

        public Pursue           pursue          = Pursue.Default;
        public float            pursueWeight    = 1.0f;
        public float            pursueBlend     = 0.0f;

        public float            frameBlending   = 0.0f;
        public float            contextBlurring = 1.0f;

        #endregion

        public static EnemyBehaviour Default { get { return ScriptableObject.CreateInstance<EnemyBehaviour>(); } }
    }
}