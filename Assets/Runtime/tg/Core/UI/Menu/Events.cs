namespace tg.ui.menu.events
{
    using tg.events;

    public struct OpenMenuEvent : IEvent
    {
        public string   name;
        public bool     push;
    }

    public struct CloseMenuEvent : IEvent
    {
    }
}
