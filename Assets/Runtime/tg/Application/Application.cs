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
    using tg.enemy;

    namespace entities
    {
        [UpdateInGroup(typeof(InitializationSystemGroup))]
        [CreateAfter(typeof(EventQueue))]
        public partial class Applicaiton : SystemBase, IEventListener<Applicaiton>
        {
            protected override void OnCreate()
	        {
                EventQueue.subscribe(this);
	        }

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
                var op5 = Addressables.LoadAssetAsync<ThemeStyleSheet>("tg.ui.theme");
                op5.Completed += operation => { if(operation.Status == AsyncOperationStatus.Succeeded) { appData.uiTheme = operation.Result; } };

                var loadOp = Addressables.ResourceManager.CreateGenericGroupOperation(new List<AsyncOperationHandle> { op1, op2, op3, op4, op5 }, true);
                loadOp.Completed += operation =>
                {
                    if(operation.Status == AsyncOperationStatus.Succeeded)
                    {
                        World.DefaultGameObjectInjectionWorld.EntityManager.AddComponentObject(World.DefaultGameObjectInjectionWorld.GetExistingSystem<Applicaiton>(), appData);

                        // activate 'tg.input.actions' 
                        appData.inputActions.Enable();
                        EventQueue.publish(new ApplicationInitializedEvent {});
                    }
                };

                //reqeust.load(new UntypedWeakReferenceId[]
                //{
                //    appData.playerPrefab,
                //    appData.enemyPrefab,
                //    //appData.abilities,
                //    appData.defaultEnemyBehaviour,
                //    appData.inputActions,
                //    appData.uiTheme,
                //},
                //(hadErrors) =>
                //{
                //    if(hadErrors) { throw new System.Exception("Failed to load application data."); }

                //    // activate 'tg.input.actions' 
                //    appData.inputActions.result.Enable();

                //    EventQueue.publish(new ApplicationInitializedEvent { appData = appData });
                //});
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

            public ThemeStyleSheet  uiTheme;
        }
    }
}

