namespace tg.application.events
{
    using tg.events;

    /// <summary>
    /// Fired by the app manager, once the initial application state is setup.
    /// </summary>
    public struct ApplicationInitializedEvent : IEvent
    {
        public ApplicationData appData;
    }

    public struct RequestApplicationQuitEvent : IEvent {}
}