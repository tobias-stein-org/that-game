using Unity.Entities;

namespace tg.enemy
{
    public struct EnemyInputData : IComponentData
    {
        /// <summary>
        /// Contains the last index (decsion) of the previous behaviour context.
        /// </summary>
        public byte    lastBehaviourContextDecision;
    }
}