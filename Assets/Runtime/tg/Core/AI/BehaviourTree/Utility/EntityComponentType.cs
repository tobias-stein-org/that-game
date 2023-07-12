using UnityEngine;

using Unity.Entities;

namespace tg.ai.behaviour.tree
{
    [System.Serializable]
    public struct EntityComponentType
    {
        public string type;

        [System.NonSerialized]
        private ComponentType? cached;

        public EntityComponentType(System.Type type)
        {
            this.type = type != null ? type.AssemblyQualifiedName : "";
            this.cached = null;
        }

        public static implicit operator ComponentType(EntityComponentType componentType)
        {
            if(!componentType.cached.HasValue) { componentType.cached = new ComponentType(System.Type.GetType(componentType.type)); }
            return componentType.cached.Value;
        }

        public static implicit operator EntityComponentType(ComponentType componentType)
        {
            return new EntityComponentType(componentType.GetManagedType());
        }
    }
}
