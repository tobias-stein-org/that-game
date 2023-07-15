using UnityEngine.UIElements;

namespace tg.ui.view
{
    using tg.ui.menu;

    public class SETTINGSController : IViewController
    {
        public void activated(VisualElement view)
        {
            view.Q<Button>("back").clicked += this.back;
        }

        public void deactivated(VisualElement view)
        {
            view.Q<Button>("back").clicked -= this.back;
        }

        private void back()
        {
            Menu.close();
        }
    }
}