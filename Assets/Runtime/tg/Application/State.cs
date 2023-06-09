using System.Collections.Generic;

using Unity.Entities;

namespace tg.application
{
    using tg.events;


    [System.Flags]
    public enum ApplicationStateMask
    {
        Never                       = 0,

        AllowRunWhenMenuOpen        = 1 << 0,

        AllowRunWhenPaused          = 1 << 1,

        AllowRunWhenInGame          = 1 << 2,

        AllowRunWhenGameOver        = 1 << 3,

        AllowRunWhenLoading         = 1 << 4,

        AllowRunWhenInitializing    = 1 << 5,

        AllowRunWhenQuitting        = 1 << 6,

        Default                     = AllowRunWhenInGame
    }

    /// <summary>
    /// A game state filter annotation that determine when a certain application system is allowed to update.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
    public class ApplicationStateFilter : System.Attribute
    {
        private ApplicationStateMask   mask;

        public ApplicationStateFilter(ApplicationStateMask mask = ApplicationStateMask.Default) { this.mask = mask; }

        public bool has(ApplicationStateMask flag) { return (this.mask & flag) != 0; }
    }


    /// <summary>
    /// Application state manager is responsible to create and destroy game state componentes that effect the RequireForUpdate state query
    /// of all application systems.
    /// </summary>
    [CreateAfter(typeof(EventQueue))]
    public partial class StateManager : SystemBase, IEventListener<StateManager>
    {
        /// <summary>
        /// Called by ISystem application systems to derive the RequireForUpdate state based on their defined GameStateFilter.
        /// </summary>
        /// <param name="system"></param>
        /// <returns></returns>
        public static EntityQuery state(ISystem system)     { return StateManager.state(system.GetType()); }

        /// <summary>
        /// Called by SystemBAse application systems to derive the RequireForUpdate state based on their defined GameStateFilter.
        /// </summary>
        /// <param name="system"></param>
        /// <returns></returns>
        public static EntityQuery state(SystemBase system)  { return StateManager.state(system.GetType()); }


        private static EntityQuery state(System.Type system)
        {
            var appStateFilter = System.Attribute.GetCustomAttribute(system, typeof(ApplicationStateFilter)) as ApplicationStateFilter;
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

            return World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntityQuery(new EntityQueryDesc { Any = allow.ToArray(), None = disallow.ToArray() });
        }

        protected override void OnCreate()
        {
            EventQueue.subscribe(this);
        }

        protected override void OnUpdate()
        {
            this.Enabled = false;
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

