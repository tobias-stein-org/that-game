using UnityEngine;

namespace tg.ability
{
    using tg.ability.entities;

    public class AbilityDescription : ScriptableObject
    {
        public const string     label   = "ability";

        public GameObject       abilityPrefab;

        public AbilityMeta      meta;
    }
}