using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace tg.test.events
{
    using tg.events;

    public class CompAAuthoring : MonoBehaviour
    {
        public int foo;

        class Baker : Baker<CompAAuthoring>
        {
            public override void Bake(CompAAuthoring authoring)
            {
                var entity = this.GetEntity(TransformUsageFlags.Dynamic);
                this.AddComponent(entity, new CompA
                {
                    foo = authoring.foo
                });
            }
        }
    }

    public struct CompA : IComponentData
    {
        public int foo;
    }
}
