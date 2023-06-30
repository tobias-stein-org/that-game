using UnityEngine;
using Unity.Entities;

namespace tg.ability
{
    using tg.assets;
    using tg.ability.entities;

#if UNITY_EDITOR
    public class AbilityAuthering : MonoBehaviour
    {
        public AbilityDescription  description;

        //class Baker : Baker<AbilityAuthering>
        //{
        //    public override void Bake(AbilityAuthering authoring)
        //    {
        //        DependsOn(authoring.description);

        //        if(authoring.description == null) { return; }

        //        var abilityEntity = GetEntity(TransformUsageFlags.None);
        //        {
        //            AddComponent(abilityEntity, new ComponentTypeSet(new ComponentType[]
        //            {
        //                typeof(AbilityData),
        //                typeof(AbilityMeta),
        //                typeof(AbilityLearned)
        //            }));
                    
        //            SetComponent<AbilityData>(abilityEntity, new AbilityData
        //            {
        //                abilityPrefab = new WeakAssetReference<GameObject>(authoring.description.abilityPrefab)
        //            });
        //            SetComponent<AbilityMeta>(abilityEntity, authoring.description.meta);
        //            SetComponentEnabled<AbilityLearned>(abilityEntity, false);
        //        }
        //    }
        //}
    }
#endif
}