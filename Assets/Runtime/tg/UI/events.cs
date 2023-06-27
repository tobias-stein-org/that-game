namespace tg.ui.events
{
    using tg.events;

    public struct ShowViewEvent : IEvent
    {
        public string   name;
        //public bool     activateController;
    }

    public struct HideViewEvent : IEvent
    {
        public string   name;
        //public bool     deactivateController;
    }

    public struct ToggleViewEvent : IEvent
    {
        public string   name;
        //public bool     toggleController;
    }
}