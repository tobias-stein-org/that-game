using System;
using System.Collections.Generic;

using Unity.Entities;

namespace tg.application
{
    using tg.debug;
    using tg.events;
    
    using tg.application.events;
    using tg.game.events;

    namespace entities
    {
        using tg.ui.entities;

        [Flags]
        public enum ApplicationStateMask
        {
            /// <summary>
            /// A dummy state that will never occur. This can be used to programatically initialize a system OnCreate
            /// to never run.
            /// </summary>
            Never                       = 0,

            AllowRunWhenMenuOpen        = 1 << 0,

            AllowRunWhenPaused          = 1 << 1,

            AllowRunWhenInGame          = 1 << 2,

            /// <summary>
            /// Allow a system to run when the GameOver state is reached.
            /// </summary>
            AllowRunWhenGameOver        = 1 << 3,

            AllowRunWhenLoading         = 1 << 4,

            /// <summary>
            /// Allow a system to run while the application is initializing.
            /// </summary>
            AllowRunWhenInitializing    = 1 << 5,

            /// <summary>
            /// Allow a system to run when the application is quitting.
            /// </summary>
            AllowRunWhenQuitting        = 1 << 6,

            Default                     = AllowRunWhenInGame
        }

        /// <summary>
        /// A game state filter annotation that determine when a certain application system is allowed to update.
        /// </summary>
        [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
        public class ApplicationStateFilter : Attribute
        {
            public readonly ApplicationStateMask    mask;
            public readonly bool                    strict;

            /// <summary>
            /// 
            /// </summary>
            /// <param name="mask"></param>
            /// <param name="strict">If true, the mask has to exacly match the current active and inactive states. If false at least one active state has to match the mask.</param>
            public ApplicationStateFilter(ApplicationStateMask mask = ApplicationStateMask.Default, bool strict = true)
            {
                this.mask   = mask;
                this.strict = strict;
            }

            public bool has(ApplicationStateMask flag) { return (this.mask & flag) != 0; }
        }

        /// <summary>
        /// Application state manager is responsible to create and destroy game state componentes that effect the RequireForUpdate state query
        /// of all application systems.
        /// </summary>
        [CreateAfter(typeof(EventQueue))]
        [CreateBefore(typeof(Applicaiton))]
        [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
        public partial class StateManager : SystemBase, IEventListener<StateManager>
        {
            /// <summary>
            /// Called by ISystem application systems to derive the RequireForUpdate state based on their defined GameStateFilter.
            /// </summary>
            /// <param name="system"></param>
            /// <returns></returns>
            public static EntityQuery state(ISystem system) { return StateManager.state(system.GetType()); }

            /// <summary>
            /// Called by SystemBAse application systems to derive the RequireForUpdate state based on their defined GameStateFilter.
            /// </summary>
            /// <param name="system"></param>
            /// <returns></returns>
            public static EntityQuery state(SystemBase system) { return StateManager.state(system.GetType()); }

            private static EntityQuery state(System.Type system)
            {
                var appStateFilter = Attribute.GetCustomAttribute(system, typeof(ApplicationStateFilter)) as ApplicationStateFilter;
                Unity.Assertions.Assert.IsNotNull(appStateFilter, $"System {system.Name} is missing a {typeof(ApplicationStateFilter).Name} annotation.");

                var filter2State = new Dictionary<ApplicationStateMask, ComponentType>
                {
                    { ApplicationStateMask.Never,                      typeof(NeverState) },
                    { ApplicationStateMask.AllowRunWhenInitializing,   typeof(InitializingState) },
                    { ApplicationStateMask.AllowRunWhenQuitting,       typeof(QuittingState) },
                    { ApplicationStateMask.AllowRunWhenPaused,         typeof(PausedState) },
                    { ApplicationStateMask.AllowRunWhenLoading,        typeof(LoadingState) },
                    { ApplicationStateMask.AllowRunWhenInGame,         typeof(InGameState) },
                    { ApplicationStateMask.AllowRunWhenMenuOpen,       typeof(MenuOpenState) },
                    { ApplicationStateMask.AllowRunWhenGameOver,       typeof(GameOverState) },
                };

                var allow    = new List<ComponentType>(4);
                var disallow = new List<ComponentType>(4);
                void check(ApplicationStateMask flag, ComponentType state) => (appStateFilter.has(flag) ? allow : disallow).Add(state);

                foreach(var (flag, state) in filter2State) { check(flag, state); }

                return World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc
                {
                    All     = appStateFilter.strict ? allow.ToArray()               : Array.Empty<ComponentType>(),
                    None    = appStateFilter.strict ? disallow.ToArray()            : Array.Empty<ComponentType>(),
                    Any     = appStateFilter.strict ? Array.Empty<ComponentType>()  : allow.ToArray(),
                    Options = EntityQueryOptions.IncludeSystems
                });
            }


            private EntityQuery checkMenuOpened;
            private EntityQuery checkMenuClosed;

            protected override void OnCreate()
            {
                this.checkMenuOpened = this.EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIViewMenu), typeof(UIViewShow) } });
                this.checkMenuOpened.SetChangedVersionFilter(typeof(UIViewShow));
                this.checkMenuClosed = this.EntityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(UIViewMenu) }, Disabled = new ComponentType[] { typeof(UIViewShow) } });
                this.checkMenuClosed.SetChangedVersionFilter(typeof(UIViewShow));

                this.RequireAnyForUpdate(new EntityQuery[]
                {
                    this.checkMenuOpened,
                    this.checkMenuClosed
                });

                EventQueue.subscribe(this);

                // once this system becomes active, it will put the application in the initialization state.
                this.EntityManager.AddComponent<InitializingState>(this.SystemHandle);
            }

            protected override void OnUpdate()
            {
                if(!this.checkMenuOpened.IsEmpty)
                {
                    var numOpenMenus = SystemAPI.QueryBuilder().WithAll<UIViewShow, UIViewMenu>().Build().CalculateEntityCount();
                    if(numOpenMenus > 0 && !this.EntityManager.HasComponent<MenuOpenState>(this.SystemHandle))
                    {
                        this.EntityManager.AddComponent<MenuOpenState>(this.SystemHandle);
                        // pauses all animations and physics
                        UnityEngine.Time.timeScale = 0f;

                        // note: apparently this is necessary to gurantee all InputAction callback un/subscriptions are still updated.
                        // seeting the timeScale to zero seems to disable this functionality so a manual update seems to be required.
                        UnityEngine.InputSystem.InputSystem.Update();
                    }
                }

                if(!this.checkMenuClosed.IsEmpty)
                {
                    var numOpenMenus = SystemAPI.QueryBuilder().WithAll<UIViewShow, UIViewMenu>().Build().CalculateEntityCount();
                    if(numOpenMenus == 0 && this.EntityManager.HasComponent<MenuOpenState>(this.SystemHandle))
                    {
                        this.EntityManager.RemoveComponent<MenuOpenState>(this.SystemHandle);
                        if(!this.EntityManager.HasComponent<PausedState>(this.SystemHandle))
                        {
                            // resume all animations and physics
                            UnityEngine.Time.timeScale = 1f;
                        }
                    }
                }
            }

            [ConsoleCommand("pause", "Pause or unpause the application.")]
            static void pauseCmd() { EventQueue.publish(new PauseEvent { }); }

            void onPauseEvent(PauseEvent e)
            {
                // unpause
                if(this.EntityManager.HasComponent<PausedState>(this.SystemHandle)) 
                {
                    this.EntityManager.RemoveComponent<PausedState>(this.SystemHandle);
                }
                else // pause
                {
                    this.EntityManager.AddComponent<PausedState>(this.SystemHandle);
                }

            }

            /// <summary>
            /// Listen to the applicaton's initialized state and transition to the "MenuOpen" state.
            /// </summary>
            /// <param name="e"></param>
            void onApplicationInitializedEvent(ApplicationInitializedEvent e)
            {
                this.EntityManager.RemoveComponent<InitializingState>(this.SystemHandle);
                // a little bit sloppy, but we need to ensure there is always a state attached to the StateManager class. So after initial
                // game data has been loaded we will always transition into the "menu open" state.
                this.EntityManager.AddComponent<MenuOpenState>(this.SystemHandle);
            }

            /// <summary>
            /// Set the "Quitting" state. This will be the final application state before termination.
            /// </summary>
            /// <param name="e"></param>
            void onApplicationQuitEvent(RequestApplicationQuitEvent e)
            {
                using(var states = this.EntityManager.GetComponentTypes(this.SystemHandle))
                {
                    foreach(var state in states) { this.EntityManager.RemoveComponent(this.SystemHandle, state); }
                }

                this.EntityManager.AddComponent<QuittingState>(this.SystemHandle);
            }

            void onNewGameStartedEvent(NewGameStartedEvent e)
            {
                this.EntityManager.RemoveComponent<GameOverState>(this.SystemHandle);
                this.EntityManager.AddComponent<InGameState>(this.SystemHandle);
            }

            void onGameOverEvent(GameOverEvent e)
            {
                this.EntityManager.AddComponent<GameOverState>(this.SystemHandle);
            }

            #region Game States


            private struct NeverState : IComponentData {}

            private struct MenuOpenState : IComponentData {}

            private struct InGameState : IComponentData {}

            private struct PausedState : IComponentData {}

            private struct GameOverState : IComponentData {}

            private struct LoadingState : IComponentData {}

            private struct InitializingState : IComponentData {}

            private struct QuittingState : IComponentData {}

            #endregion
        }
    }
}

