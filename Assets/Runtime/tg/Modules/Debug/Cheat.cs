using Unity.Entities;

using UnityEngine.InputSystem;

namespace tg.debug
{
    using tg.events;
    using tg.application;

    using tg.level.events;
    using tg.player.events;
    using tg.application.events;

    public partial class Cheat : SystemBase
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate<ApplicationDataLoaded>();
        }

        protected override void OnUpdate()
        {
            var debugActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Debug");

            debugActions.FindAction("quit").performed += onQuit;
            debugActions.FindAction("spawn_player").performed += onSpawnPlayer;
            debugActions.FindAction("new_level").performed += onNewLevel;

            this.Enabled = false;
        }

        void onApplicationQuitEvent(ApplicationQuitEvent e)
        {
            var debugActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Debug");
            
            debugActions.FindAction("quit").performed -= onQuit;
            debugActions.FindAction("spawn_player").performed -= onSpawnPlayer;
            debugActions.FindAction("new_level").performed -= onNewLevel;
        }

        private void onNewLevel(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new RequestNewLevelEvent {});
        }

        private void onSpawnPlayer(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new SpawnPlayerRequestEvent { });   
        }

        private void onQuit(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new ApplicationQuitEvent {});
        }
    }
}
