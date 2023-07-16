namespace tg.application.events
{
    using tg.application.entities;
    using tg.events;
    
    /// <summary>
    /// Fired by the app manager, once the initial application state is setup.
    /// </summary>
    public struct ApplicationInitializedEvent : IEvent
    {
        public ApplicationData  data;
    }

    public struct RequestApplicationQuitEvent : IEvent {}


    public struct PauseEvent : IEvent {}
}