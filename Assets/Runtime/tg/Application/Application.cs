using UnityEngine;
using UnityEngine.InputSystem;

using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;


namespace tg.application
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct ApplicationManager : ISystem, ISystemStartStop
    {
        void OnCreate(ref SystemState state)
	    {
            state.RequireForUpdate<ApplicationData>();
	    }

	    void OnDestroy(ref SystemState state)
	    {

	    }

	    void OnUpdate(ref SystemState state)
	    {
            var appData = SystemAPI.GetSingleton<ApplicationData>();

            if(appData.inputActions.LoadingStatus != ObjectLoadingStatus.Completed) { return; }

            // activa 'tg.input.actions' 
            appData.inputActions.Result.Enable();

            state.Enabled = false;
	    }

        public void OnStartRunning(ref SystemState state)
        {
            // Initialize app data ...
            var appData = SystemAPI.GetSingleton<ApplicationData>();

            // load player prefab ...
            appData.playerPrefab.LoadAsync();
            appData.inputActions.LoadAsync();
        }

        public void OnStopRunning(ref SystemState state)
        {
        }

        void onApplicationQuitEvent(ApplicationQuitEvent e)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
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
        public GameObject           playerPrefab;

        public InputActionAsset     inputActions;

#if UNITY_EDITOR
        class Baker : Baker<Application>
        {
            public override void Bake(Application authoring)
            {
                this.DependsOn(authoring.playerPrefab);
                this.DependsOn(authoring.inputActions);

                if(authoring.playerPrefab == null) { return; }
                if(authoring.inputActions == null) { return; }

                var appData = GetEntity(TransformUsageFlags.None);
                AddComponent<ApplicationData>(appData, new ApplicationData
                {
                    playerPrefab    = new WeakObjectReference<GameObject>(authoring.playerPrefab),
                    inputActions    = new WeakObjectReference<InputActionAsset>(authoring.inputActions)
                });
            }
        }
#endif
    }

    [System.Serializable]
    public struct ApplicationData : IComponentData
    {
        public WeakObjectReference<GameObject>          playerPrefab;

        public WeakObjectReference<InputActionAsset>    inputActions;
    }

    public struct LoadApplicationData : IComponentData {}
}

