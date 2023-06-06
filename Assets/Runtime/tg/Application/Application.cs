using UnityEngine;
using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;
using UnityEngine.Scripting;

namespace tg.application
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ApplicationManager : ISystem
    {
        private EntityQuery query;

        void OnCreate(ref SystemState state)
	    {
            state.EntityManager.AddComponentData<LoadApplicationData>(state.SystemHandle, new LoadApplicationData {});
            state.RequireForUpdate<ApplicationData>();
            state.RequireForUpdate<LoadApplicationData>();
	    }

	    void OnDestroy(ref SystemState state)
	    {
            this.query.Dispose();
	    }

	    void OnUpdate(ref SystemState state)
	    {
            // Initialize app data ...
            var appData = SystemAPI.GetSingleton<ApplicationData>();

            // load player prefab ...
            appData.playerPrefab.LoadAsync();

            state.EntityManager.RemoveComponent<LoadApplicationData>(state.SystemHandle);
	    }
    }

    /// <summary>
    /// Contains general settings and resource references used in this application.
    /// </summary>
    public class Application : MonoBehaviour
    {
        /// <summary>
        /// Player prefab asset.
        /// </summary>
        public GameObject   playerPrefab;

#if UNITY_EDITOR
        class Baker : Baker<Application>
        {
            public override void Bake(Application authoring)
            {
                this.DependsOn(authoring.playerPrefab);

                if(authoring.playerPrefab == null) { return; }

                var appData = GetEntity(TransformUsageFlags.None);
                AddComponent<ApplicationData>(appData, new ApplicationData
                {
                    playerPrefab = new WeakObjectReference<GameObject>(authoring.playerPrefab)
                });
            }
        }
#endif
    }

    [System.Serializable]
    public struct ApplicationData : IComponentData
    {
        public WeakObjectReference<GameObject>   playerPrefab;
    }

    public struct LoadApplicationData : IComponentData {}
}

