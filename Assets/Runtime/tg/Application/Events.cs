namespace tg.application.events
{
    using tg.events;

    public struct ApplicationDataLoadedEvent : IEvent
    {
        public ApplicationData appData;
    }

    public struct ApplicationQuitEvent : IEvent {}
}