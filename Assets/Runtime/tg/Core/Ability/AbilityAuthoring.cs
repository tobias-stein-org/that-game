using UnityEngine;
using Unity.Entities;

namespace tg.ability
{
    public class AbilityAuthering : MonoBehaviour
    {
        public AbilityDescription  description;

        class Baker : Baker<AbilityAuthering>
        {
            public override void Bake(AbilityAuthering authoring)
            {
                DependsOn(authoring.description);

                if(authoring.description == null) { return; }

                var abilityEntity = GetEntity(TransformUsageFlags.None);
                {
                    AddComponent<AbilityData>(abilityEntity, new AbilityData
                    {
                        ability = new assets.WeakAssetReference<AbilityDescription>(authoring.description)
                    });
                }
            }
        }
    }
}