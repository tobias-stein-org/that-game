using Unity.Entities;

using UnityEngine.InputSystem;

namespace tg.debug
{
    using tg.events;
    using tg.application;
    using tg.player;

    using tg.level.events;
    using tg.player.events;
    using tg.application.events;


    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame)]
    public partial class Cheat : SystemBase
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate(StateManager.state(this));
        }

        protected override void OnStartRunning()
        {
            var debugActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Debug");

            debugActions.FindAction("quit").performed += onQuit;
            debugActions.FindAction("spawn_player").performed += onSpawnPlayer;
            debugActions.FindAction("kill_player").performed += onKillPlayer;
            debugActions.FindAction("new_level").performed += onNewLevel;
        }

        protected override void OnStopRunning()
        {
            var debugActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Debug");
            
            debugActions.FindAction("quit").performed -= onQuit;
            debugActions.FindAction("spawn_player").performed -= onSpawnPlayer;
            debugActions.FindAction("kill_player").performed -= onKillPlayer;
            debugActions.FindAction("new_level").performed -= onNewLevel;
        }

        private void onNewLevel(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new RequestNewLevelEvent {});
        }

        private void onSpawnPlayer(InputAction.CallbackContext obj)
        {
            // only allow to spawn a single player entity
            if(World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(Player) } }).CalculateEntityCount() == 0)
            {
                EventQueue.publish(new SpawnPlayerRequestEvent {});   
            }
        }

        private void onKillPlayer(InputAction.CallbackContext obj)
        {
            foreach(var player in SystemAPI.Query<Player>())
            {
                EventQueue.publish(new KillPlayerEvent { player = player });   
            }
        }

        private void onQuit(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new RequestApplicationQuitEvent {});
        }
    }
}
