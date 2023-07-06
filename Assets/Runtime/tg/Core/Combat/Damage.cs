using Unity.Entities;
using Unity.Collections;

using FrameDamageBuffer = Unity.Collections.FixedList512Bytes<tg.combat.Damage>;

namespace tg.combat
{
    public struct Damage
    {
        public enum Type
        {
            Raw,

            Weapon_Melee,
            Weapon_Range,

            Ability_Physical,
            Ability_Magical
        }

        public Entity               source;

        public FixedString64Bytes   name;

        public Type                 type;

        public float                value;
    }

    namespace entities
    {
        public struct Damagaeble : IComponentData, IEnableableComponent
        {
            /// <summary>
            /// Raw accumulated frame damage.
            /// </summary>
            public FrameDamageBuffer    frameDamage;

            /// <summary>
            /// 
            /// </summary>
            public float                resolvedDamage;
        }
    }
}
