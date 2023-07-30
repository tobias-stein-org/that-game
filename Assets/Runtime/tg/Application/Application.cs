using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

using Unity.Entities;

namespace tg.application
{
    using tg.events;
    using tg.application.events;
    using tg.ui.menu;
    using tg.enemy;

    namespace entities
    {
        [UpdateInGroup(typeof(InitializationSystemGroup))]
        [CreateAfter(typeof(EventQueue))]
        public partial class Applicaiton : SystemBase, IEventListener
        {
            protected override void OnCreate()
	        {
                EventQueue.subscribe(this);

                UnityEngine.Application.wantsToQuit        -= this.wantsToQuit;
                UnityEngine.Application.wantsToQuit        += this.wantsToQuit;

#if UNITY_EDITOR
                UnityEditor.EditorApplication.playModeStateChanged -= this.onPlaymodeChange;
                UnityEditor.EditorApplication.playModeStateChanged += this.onPlaymodeChange;
#endif
            }

            private bool wantsToQuit()
            {
                UnityEngine.Application.wantsToQuit -= this.wantsToQuit;
                EventQueue.publish(new RequestApplicationQuitEvent { });

                return true;
            }

#if UNITY_EDITOR
            private void onPlaymodeChange(UnityEditor.PlayModeStateChange state)
            {
                if(state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    UnityEditor.EditorApplication.playModeStateChanged -= this.onPlaymodeChange;
                    EventQueue.publish(new RequestApplicationQuitEvent { });
                }
            }
#endif

            protected override void OnUpdate()
	        {
                this.Enabled = false;
	        }

            protected override void OnStartRunning()
            {
                // Initialize app data ...

                var appData = new ApplicationData();
                var op1 = Addressables.LoadAssetAsync<GameObject>("tg.player");
                op1.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded)
                {
                    appData.playerPrefab = operation.Result; }
                };
                var op2 = Addressables.LoadAssetAsync<GameObject>("tg.enemy");
                op2.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.enemyPrefab = operation.Result; } };
                var op3 = Addressables.LoadAssetAsync<EnemyBehaviour>("tg.enemy.defaultBehaviour");
                op3.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.defaultEnemyBehaviour = operation.Result; } };
                var op4 = Addressables.LoadAssetAsync<InputActionAsset>("tg.input.actions");
                op4.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.inputActions = operation.Result; } };
                var op5 = Addressables.LoadAssetAsync<PanelSettings>("tg.ui.settings");
                op5.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.uiSettings = operation.Result; } };

                var loadOp = Addressables.ResourceManager.CreateGenericGroupOperation(new List<AsyncOperationHandle> { op1, op2, op3, op4, op5 }, true);
                loadOp.Completed += operation =>
                {
                    if(operation.Status == AsyncOperationStatus.Succeeded)
                    {
                        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentObject(World.DefaultGameObjectInjectionWorld.GetExistingSystem<Applicaiton>(), appData);

                        // activate 'tg.input.actions' 
                        appData.inputActions.Enable();

                        EventQueue.publish(new ApplicationInitializedEvent { data = appData });

                        Menu.show(menus.MAIN_MENU, false);
                    }
                };
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

        public class ApplicationData : IComponentData
        {
            public GameObject       playerPrefab;

            public GameObject       enemyPrefab;

            public EnemyBehaviour   defaultEnemyBehaviour;

            public InputActionAsset inputActions;

            public PanelSettings    uiSettings;
        }
    }
}

