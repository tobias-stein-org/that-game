using System;

using UnityEngine;

using Unity.Entities;
using Unity.Collections;
using Unity.Entities.Serialization;

namespace tg.ability
{
    using tg.application;
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
        public WeakAssetReference<GameObject>  abilityPrefab;
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


    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial struct AbilitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate(StateManager.state(state.WorldUnmanaged.GetUnsafeSystemRef<AbilitySystem>(state.SystemHandle)));
            state.RequireForUpdate<AbilityMeta>();
        }

        public void OnUpdate(ref SystemState state)
        {
            foreach(var meta in SystemAPI.Query<AbilityMeta>())
            {
                Debug.Log($"found ability {meta.name}");
            }

            state.Enabled = false;
        }
    }
}
