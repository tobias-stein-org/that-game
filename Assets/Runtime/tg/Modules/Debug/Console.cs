
using UnityEngine;

using Unity.Entities;

namespace tg.debug
{
    using tg.application;
    using tg.events;
    using tg.ui.events;
    using UnityEngine.InputSystem;

    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenInGame | ApplicationStateMask.AllowRunWhenMenuOpen | ApplicationStateMask.AllowRunWhenPaused, false)]
    public partial class Console : SystemBase
    {
        protected override void OnCreate()
        {
            this.RequireForUpdate(StateManager.state(this));   
        }

        protected override void OnUpdate()
        {
            var debugActions = SystemAPI.GetSingleton<ApplicationData>().inputActions.result.FindActionMap("Debug");

            debugActions.FindAction("console").performed += toggleConsole;

            this.Enabled = false;
        }

        private void toggleConsole(InputAction.CallbackContext obj)
        {
            EventQueue.publish(new ToggleViewEvent { name = "console" });
        }
    }
}
