using System.Linq;

using UnityEngine;

using Unity.Entities;
using Unity.Collections;

namespace tg.physics
{
    using tg.events;

    /// <summary>
    /// A static class that maintains a global look up table to map Collider2D objects to their parent Entity.
    /// </summary>
    public static class collider2entity
    {
        internal static NativeHashMap<int, Entity>  mapping;

        public static Entity toEntity(Collider2D collider)
        {
            //Unity.Assertions.Assert.IsTrue(mapping.IsCreated, "Collider to entity mapping is only available at runtime.");
            return collider != null && mapping.TryGetValue(collider.GetInstanceID(), out Entity entity) ? entity : Entity.Null;
        }
    }

    namespace entities
    {
        using tg.application.entities;

        /// <summary>
        /// This component should be added to entities with a companion GameObject that participates in
        /// the managed Physics realm.
        /// </summary>
        public class WithManagedCollider : IComponentData
        {
            public Entity       entity;
            public GameObject   gameOb;
        }

        /// <summary>
        /// Whenever an entity with the 'WithManagedCollider' is created this system attempts to collected
        /// all Collider2D managed components from the companion GameObject and adds them to a global look
        /// up table.
        /// </summary>
        [CreateAfter(typeof(EventQueue))]
        [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
        public partial class EnityColliderMapper : SystemBase
        {
            private EntityQuery withManagedCollider;

            protected override void OnCreate()
            {
                if(collider2entity.mapping.IsCreated) { collider2entity.mapping.Dispose(); }
                collider2entity.mapping = new NativeHashMap<int, Entity>(128, Allocator.Persistent);

                this.withManagedCollider = new EntityQueryBuilder(Allocator.Temp).WithAll<WithManagedCollider>().Build(this);
                this.withManagedCollider.SetChangedVersionFilter(typeof(WithManagedCollider));

                this.RequireForUpdate(this.withManagedCollider);
            }

            protected override void OnDestroy()
            {
                collider2entity.mapping.Dispose();
            }

            protected override void OnUpdate()
            {
                var results = this.withManagedCollider.ToComponentDataArray<WithManagedCollider>();
                foreach(var data in results)
                {
                    foreach(var collider in data.gameOb.GetComponentsInChildren<Collider2D>().Select(collider => collider.GetInstanceID()))
                    {
                        collider2entity.mapping[collider] = data.entity;
                    }       
                }
            }
        }
    }
}