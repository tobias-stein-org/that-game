using UnityEngine;
using UnityEditor;

namespace tg.ability
{
    using tg.ability.entities;

    public class AbilityDescription : ScriptableObject
    {
        public GameObject       abilityPrefab;

        public AbilityMeta      meta;
    }
}