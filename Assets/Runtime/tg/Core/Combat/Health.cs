using Unity.Entities;

namespace tg.combat
{
    namespace entities
    {
        /// <summary>
        /// Not that disabling this component will kill an entity.
        /// </summary>
        public struct Health : IComponentData, IEnableableComponent
        {
            public float    maxHealth;
            public float    health;
        }
    }
}
