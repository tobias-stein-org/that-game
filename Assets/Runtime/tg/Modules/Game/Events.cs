namespace tg.game.events
{
    using tg.events;

    public struct StartGameEvent : IEvent
    {
    }

    public struct NewGameStartedEvent : IEvent {}

    public struct GameOverEvent : IEvent {}
}