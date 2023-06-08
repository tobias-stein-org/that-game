using UnityEngine;
using UnityEngine.InputSystem;

using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;


namespace tg.application
{
    using tg.events;
    using tg.assets;
    using tg.application.events;

    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [CreateAfter(typeof(EventQueue))]
    public partial struct ApplicationManager : ISystem, ISystemStartStop, IEventListener<ApplicationManager>
    {
        void OnCreate(ref SystemState state)
	    {
            state.RequireForUpdate<ApplicationData>();

            EventQueue.subscribe(state.WorldUnmanaged.GetUnsafeSystemRef<ApplicationManager>(state.SystemHandle));
	    }

	    void OnDestroy(ref SystemState state)
	    {

	    }

	    void OnUpdate(ref SystemState state)
	    {
            state.Enabled = false;
	    }
       
        public void OnStartRunning(ref SystemState state)
        {
            // Initialize app data ...
            var appData         = SystemAPI.GetSingleton<ApplicationData>();
            var appDataEntity   = SystemAPI.GetSingletonEntity<ApplicationData>();

            reqeust.load(new UntypedWeakReferenceId[]
            {
                appData.playerPrefab,
                appData.inputActions
            },
            (hadErrors) =>
            {
                if(hadErrors) { throw new System.Exception("Failed to load application data."); }

                // activate 'tg.input.actions' 
                appData.inputActions.result.Enable();

                World.DefaultGameObjectInjectionWorld.EntityManager.AddComponent<ApplicationDataLoaded>(appDataEntity);

                EventQueue.publish(new ApplicationDataLoadedEvent { appData = appData });
            });
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
                    playerPrefab    = new WeakAssetReference<GameObject>(authoring.playerPrefab),
                    inputActions    = new WeakAssetReference<InputActionAsset>(authoring.inputActions),
                });
            }
        }
#endif
    }

    public struct ApplicationData : IComponentData
    {
        public WeakAssetReference<GameObject>          playerPrefab;

        public WeakAssetReference<InputActionAsset>    inputActions;
    }

    struct ApplicationDataLoaded : IComponentData {}
}

