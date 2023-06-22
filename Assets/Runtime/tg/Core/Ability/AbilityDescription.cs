using UnityEngine;
using UnityEditor;

namespace tg.ability
{
    public class AbilityDescription : ScriptableObject
    {
        public GameObject       abilityPrefab;

        public AbilityMeta      meta;
    }
}