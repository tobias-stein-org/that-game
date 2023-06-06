using Unity.Burst;
using Unity.Transforms;
using Unity.Entities;
using UnityEngine.Scripting;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Jobs;

namespace tg.player
{
	/// <summary>
	/// Dedicated system to keep player entity and it's Unity scene companion GameObject in sync.
	/// </summary>
	[UpdateInGroup(typeof(TransformSystemGroup))]
    public partial struct UpdatePlayerCompanion : ISystem
    {
		EntityQuery query;

        public void OnCreate(ref SystemState state)
        {
            query = state.GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    typeof(Player),
                    typeof(UnityEngine.Transform),
                    typeof(LocalTransform)
                }
            });
        }

		void OnDestroy(ref SystemState state)
		{
		}

		void OnUpdate(ref SystemState state)
		{
            var localTransforms = this.query.ToComponentDataListAsync<LocalTransform>(state.World.UpdateAllocator.ToAllocator, out var jobHandle);
            var inputDependency = JobHandle.CombineDependencies(state.Dependency, jobHandle);
            
			state.Dependency = new SyncPlayerCompanionTransforms
            {
                localTransforms = localTransforms
            }.Schedule(this.query.GetTransformAccessArray(), inputDependency);
		}

		[BurstCompile]
        struct SyncPlayerCompanionTransforms : IJobParallelForTransform
        {
            [ReadOnly]
			public NativeList<LocalTransform> localTransforms;
            
            public void Execute(int index, TransformAccess transform)
            {
                transform.position = this.localTransforms[index].Position;
                transform.rotation = this.localTransforms[index].Rotation;
            }
        }
    }
}

