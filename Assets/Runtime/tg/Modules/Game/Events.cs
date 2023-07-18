namespace tg.game.events
{
    using tg.events;

    public struct StartGameEvent : IEvent
    {
    }

    public struct NewGameStartedEvent : IEvent {}

    public struct GameOverEvent : IEvent
    {
        public enum Reason
        {
            PLAYER_QUIT,
            PLAYER_LOST,
            PLAYER_DONE
        }

        public Reason reason;
    }
}