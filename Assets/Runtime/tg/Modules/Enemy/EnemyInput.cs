using Unity.Entities;
using Unity.Mathematics;

namespace tg.enemy
{
    namespace entities
    {
        public struct EnemyInputData : IComponentData
        {
            public float2   look;
            public float2   move;

            /// <summary>
            /// Degrees per second.
            /// </summary>
            public float    turnSpeed;

            /// <summary>
            /// Units per second.
            /// </summary>
            public float    moveSpeed;
        }
    }
}