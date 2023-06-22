using System;

using UnityEngine;

using Unity.Entities;
using Unity.Collections;
using Unity.Entities.Serialization;

namespace tg.ability
{
    using tg.assets;

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class AbilityPropertyAttribute : Attribute
    {

    }

    [Serializable]
    public struct AbilityMeta : IComponentData
    {
        public enum Target : byte
        {
            None,
            Self,
            Point,
            Target,
            Area,
            Global
        }

        public enum Activation : byte
        {
            Instant,
            Charge,
            Channel
        }

        public  FixedString64Bytes              name;

        public  FixedString128Bytes             description;

        public  bool                            isPassive;
                                                
        public  Target                          target;
                                                
        public  Activation                      activation;
                                                
        public  float                           cost;
                                                
        public  float                           cooldown;
        public  bool                            hasCooldown { get { return this.cooldown > 0.0f; } }
                                                
        public  float                           duration;
    }

    public struct AbilityData : IComponentData
    {
        public WeakAssetReference<AbilityDescription>  ability;
    }

    public struct AbilityReady : IComponentData, IEnableableComponent {}

    public struct AbilityState : IComponentData
    {
        public enum State : byte
        {
            Ready,
            Active,
            Cooldown
        }

        public State                            state;
    }

    public struct AbilityCooldown : IComponentData
    {
        public float                            cooldown;
    }
}
