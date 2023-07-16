using System.Collections.Generic;

using UnityEngine.InputSystem;

using Unity.Entities;

namespace tg.ui.menu
{
    using tg.events;
    using tg.application.events;
    using tg.ui.events;
    using tg.ui.menu.events;
    using tg.ui.entities;

    using tg.application.entities;

    public static class menus
    {
        public const string CONSOLE     = "console";
        public const string MAIN_MENU   = "MAIN_MENU";
        public const string PAUSE_MENU  = "PAUSE_MENU";
        public const string SETTINGS    = "SETTINGS";
    }

    [CreateAfter(typeof(EventQueue))]
    [UpdateInGroup(typeof(UISystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenMenuOpen, false)]
    public partial class Menu : SystemBase, IEventListener<Menu>
    {
        private readonly Stack<string> menus = new Stack<string>(8);

        // active dialog handle

        private InputAction back = null;

        protected override void OnCreate()
        {
            this.RequireForUpdate(StateManager.state(this));
            EventQueue.subscribe(this);
        }

        protected override void OnStartRunning()
        {
            this.back.performed += this.onBackAction;
        }

        protected override void OnStopRunning()
        {
            this.back.performed -= this.onBackAction;
            this.menus.Clear();
        }

        protected override void OnUpdate()
        {
        }

        void onApplicationInitializedEvent(ApplicationInitializedEvent e)
        {
            this.back = e.data.inputActions.FindActionMap("UI").FindAction("cancel");
        }

        void onBackAction(InputAction.CallbackContext ctx)
        {
            if(!ctx.action.IsPressed())
            {
                if(this.menus.Count > 0 && this.menus.Peek() != menu.menus.MAIN_MENU)
                {
                    Menu.close();
                }
            }
        }

        void onOpenMenuEvent(OpenMenuEvent e)
        {
            if(this.menus.Count > 0)
            {
                var current = this.menus.Peek();

                // ignore this event, if menu is already opened. This might happen, is view controller events on ui view elemetns get registered multiple times.
                if(current == e.name)
                {
                    UnityEngine.Debug.LogWarning($"Received OpenMenuEvent '{e.name}' twice. Are any UI controller registered twice to view elements?");
                    return;
                }

                EventQueue.publish(new HideViewEvent { name = current });
            }
            EventQueue.publish(new ShowViewEvent { name = e.name });

            if(!e.push) { this.menus.Clear(); }
            this.menus.Push(e.name);
        }

        void onCloseMenuEvent(CloseMenuEvent e)
        {
            if(this.menus.Count > 0)
            {
                EventQueue.publish(new HideViewEvent { name = this.menus.Pop() });
                if(this.menus.Count > 0)
                {
                    EventQueue.publish(new ShowViewEvent { name = this.menus.Peek() });
                }
            }
        }

        public static void show(string menu, bool push = true)
        {
            EventQueue.publish(new OpenMenuEvent { name = menu, push = push });
        }

        public static void close()
        {
            EventQueue.publish(new CloseMenuEvent { });
        }

        public static void showDialog()
        {
        }
    }
}
