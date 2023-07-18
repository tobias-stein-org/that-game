using System.Collections.Generic;

using UnityEngine.InputSystem;

using Unity.Entities;

using UnityEngine.UIElements;

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
        public const string GAME_OVER   = "GAME_OVER";
        public const string SETTINGS    = "SETTINGS";
    }

    [CreateAfter(typeof(EventQueue))]
    [UpdateInGroup(typeof(UISystemGroup))]
    [ApplicationStateFilter(ApplicationStateMask.AllowRunWhenMenuOpen, false)]
    public partial class Menu : SystemBase, IEventListener<Menu>
    {
        private readonly Stack<string>  menus       = new Stack<string>(8);

        public const string             DIALOG      = "DIALOG";
        private static VisualElement    dialog      = null;
        private static bool             dialogOpen  = false;

        private InputAction             back        = null;


        protected override void OnCreate()
        {
            this.RequireForUpdate(StateManager.state(this));

            EventQueue.subscribe(this);
            EventQueue.publish(new SpawnViewEvent { name = Menu.DIALOG });
        }

        protected override void OnStartRunning()
        {
            this.back.performed += this.onBackAction;
        }

        protected override void OnStopRunning()
        {
            this.back.performed -= this.onBackAction;
            //this.menus.Clear();
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
            if(Menu.dialogOpen) { return; }

            EventQueue.publish(new ShowViewEvent { name = e.name });

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

            if(!e.push) { this.menus.Clear(); }
            this.menus.Push(e.name);
        }

        void onCloseMenuEvent(CloseMenuEvent e)
        {
            if(Menu.dialogOpen) { return; }

            string hide = null;
            string show = null;

            if(this.menus.Count > 0)
            {
                hide = this.menus.Pop();
                if(this.menus.Count > 0)
                {
                    show = this.menus.Peek();
                }
            }

            if(show != null) { EventQueue.publish(new ShowViewEvent { name = show }); }
            if(hide != null) { EventQueue.publish(new HideViewEvent { name = hide }); }
        }

        void onViewSpawnEvent(ViewSpawnEvent e)
        {
            if(e.view.name == Menu.DIALOG)
            {
                Menu.dialog = e.view;
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

        public static void showDialog(string title, string messge, params (string, System.Action)[] actions)
        {
            if(Menu.dialog == null)
            {
                Unity.Assertions.Assert.IsNotNull(Menu.dialog, "Dialog resource not initialized!");
            }

            Menu.dialog.Q<Label>("title").text      = title;
            Menu.dialog.Q<Label>("message").text    = messge;

            var _actions = Menu.dialog.Q<VisualElement>("actions");
            _actions.Clear();

            void addDialogAction(string name, System.Action callback = null)
            {
                var action = new Button();
                {
                    action.text = name;
                    action.clicked += () =>
                    {
                        Menu.hideDialog();
                        callback?.Invoke();
                    };
                }
                _actions.Add(action);
            }

            if(actions.Length > 0)
            {
                foreach(var action in actions)
                {
                    addDialogAction(action.Item1, action.Item2);
                }
            }
            else // add "OK" action by default
            {
                addDialogAction("OK");
            }

            // show the dialog
            EventQueue.publish(new ShowViewEvent { name = Menu.DIALOG });
            Menu.dialogOpen = true;
        }

        private static void hideDialog()
        {
            Menu.dialogOpen = false;
            EventQueue.publish(new HideViewEvent { name = Menu.DIALOG });
        }
    }
}
