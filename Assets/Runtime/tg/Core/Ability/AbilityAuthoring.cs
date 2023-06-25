using UnityEngine;
using Unity.Entities;

namespace tg.ability
{
    using tg.ability.entities;

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
                    AddComponent(abilityEntity, new ComponentTypeSet(new ComponentType[]
                    {
                        typeof(AbilityData),
                        typeof(AbilityMeta),
                        typeof(AbilityLearned)
                    }));
                    
                    SetComponent<AbilityData>(abilityEntity, new AbilityData
                    {
                        abilityPrefab = new assets.WeakAssetReference<GameObject>(authoring.description.abilityPrefab)
                    });
                    SetComponent<AbilityMeta>(abilityEntity, authoring.description.meta);
                    SetComponentEnabled<AbilityLearned>(abilityEntity, false);
                }
            }
        }
    }
}