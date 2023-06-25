using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

using Unity.Entities;
using Unity.Entities.Content;
using Unity.Entities.Serialization;


namespace tg.application
{
    using tg.events;
    using tg.assets;
    using tg.application.events;
    using tg.enemy;
    using UnityEditor;

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
                appData.enemyPrefab,
                appData.abilities,
                appData.defaultEnemyBehaviour,
                appData.inputActions
            },
            (hadErrors) =>
            {
                if(hadErrors) { throw new System.Exception("Failed to load application data."); }

                // activate 'tg.input.actions' 
                appData.inputActions.result.Enable();

                EventQueue.publish(new ApplicationInitializedEvent { appData = appData });
            });
        }

        public void OnStopRunning(ref SystemState state)
        {
        }

        void onApplicationQuitEvent(RequestApplicationQuitEvent e)
        {
            EventQueue.publish(new QuitApplicationEvent {});
        }

        void onQuitApplicationEvent(QuitApplicationEvent e)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.ExitPlaymode();
#else
            UnityEngine.Application.Quit();
#endif
        }


        /// <summary>
        /// Application internal evnet. Application manager will self induce this event once the "request quit" event has been received.
        /// Having this extra internal event, will give other systems listening to the "request quit" event to perform clean-up.
        /// </summary>
        private struct QuitApplicationEvent : IEvent {}
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

        public GameObject           enemyPrefab;

        public EnemyBehaviour       defaultEnemyBehaviour;

        /// <summary>
        /// Scene reference to the SubScene containing all default application abilities.
        /// </summary>
        public SceneAsset           abilities;

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
                if(authoring.defaultEnemyBehaviour == null) { return; }
                if(authoring.inputActions == null) { return; }

                var appData = GetEntity(TransformUsageFlags.None);
                AddComponent<ApplicationData>(appData, new ApplicationData
                {
                    playerPrefab            = new WeakAssetReference<GameObject>(authoring.playerPrefab),
                    enemyPrefab             = new WeakAssetReference<GameObject>(authoring.enemyPrefab),
                    abilities               = new WeakAssetReference<SceneAsset>(authoring.abilities),
                    defaultEnemyBehaviour   = new WeakAssetReference<EnemyBehaviour>(authoring.defaultEnemyBehaviour),
                    inputActions            = new WeakAssetReference<InputActionAsset>(authoring.inputActions),
                });;
            }
        }
#endif
    }

    public struct ApplicationData : IComponentData
    {
        public WeakAssetReference<GameObject>          playerPrefab;
        public WeakAssetReference<GameObject>          enemyPrefab;

        public WeakAssetReference<SceneAsset>          abilities;

        public WeakAssetReference<EnemyBehaviour>      defaultEnemyBehaviour;

        public WeakAssetReference<InputActionAsset>    inputActions;
    }
}

