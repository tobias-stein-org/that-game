using Unity.Entities;

using UnityEngine.InputSystem;

namespace tg.debug
{
    using tg.events;
    using tg.application.entities;
    using tg.player.entities;

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
            if(SystemAPI.ManagedAPI.TryGetSingleton<ApplicationData>(out ApplicationData data))
            {
                var debugActions = data.inputActions.FindActionMap("Debug");
                debugActions.FindAction("quit").performed += onQuit;
                debugActions.FindAction("spawn_player").performed += onSpawnPlayer;
                debugActions.FindAction("kill_player").performed += onKillPlayer;
                debugActions.FindAction("new_level").performed += onNewLevel;
            }
        }

        protected override void OnStopRunning()
        {
            if(SystemAPI.ManagedAPI.TryGetSingleton<ApplicationData>(out ApplicationData data))
            {
                var debugActions = data.inputActions.FindActionMap("Debug");
                debugActions.FindAction("quit").performed -= onQuit;
                debugActions.FindAction("spawn_player").performed -= onSpawnPlayer;
                debugActions.FindAction("kill_player").performed -= onKillPlayer;
                debugActions.FindAction("new_level").performed -= onNewLevel;
            }
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
            foreach(var (player, entity) in SystemAPI.Query<Player>().WithEntityAccess())
            {
                EventQueue.publish(new KillPlayerEvent { player = entity });   
            }
        }

        [ConsoleCommand(name: "quit", help: "Terminates current application instance.")]
        private static void onQuit(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new RequestApplicationQuitEvent {});
        }

        protected override void OnUpdate()
        {
        }
    }
}
