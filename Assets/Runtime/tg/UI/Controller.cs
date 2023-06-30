using UnityEngine.UIElements;

namespace tg.ui.view
{
    public interface IViewController
    {
        void activated(VisualElement view);
        void deactivated();
    }
}
