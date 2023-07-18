namespace tg.ui.menu.events
{
    using tg.events;

    struct OpenMenuEvent : IEvent
    {
        public string   name;
        public bool     push;
    }

    struct CloseMenuEvent : IEvent
    {
    }
}
