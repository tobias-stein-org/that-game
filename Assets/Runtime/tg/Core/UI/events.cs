using UnityEngine.UIElements;

namespace tg.ui.events
{
    using tg.events;

    public struct SpawnViewEvent : IEvent
    {
        public string name;
    }

    public struct ViewSpawnEvent : IEvent
    {
        public VisualElement    view;
    }

    public struct ShowViewEvent : IEvent
    {
        public string   name;
    }

    public struct HideViewEvent : IEvent
    {
        public string   name;
    }

    public struct ToggleViewEvent : IEvent
    {
        public string   name;
    }

    public struct ShowLoadingScreenEvent : IEvent { }

    public struct UpdateLoadingScreenProgressEvent : IEvent
    {
        public float    progress;
        public string   step;
    }

    public struct HideLoadingScreenEvent : IEvent {}
}