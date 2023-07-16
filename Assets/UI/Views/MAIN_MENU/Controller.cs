using UnityEngine.UIElements;

namespace tg.ui.view
{
    using tg.events;
    using tg.application.events;
    using tg.ui.menu;
    using tg.game.events;

    public class MAIN_MENUController : IViewController
    {
        public void activated(VisualElement view)
        {
            view.Q<Button>("play").clicked          += this.play;
            view.Q<Button>("settings").clicked      += this.settings;
            view.Q<Button>("leaderboard").clicked   += this.leaderboard;
            view.Q<Button>("quit").clicked          += this.quit;
        }

        public void deactivated(VisualElement view)
        {
            view.Q<Button>("play").clicked          -= this.play;
            view.Q<Button>("settings").clicked      -= this.settings;
            view.Q<Button>("leaderboard").clicked   -= this.leaderboard;
            view.Q<Button>("quit").clicked          -= this.quit;
        }

        private void play()
        {
            EventQueue.publish(new StartGameEvent { });
            Menu.close();
        }

        private void settings()
        {
            Menu.show(menus.SETTINGS);
        }

        private void leaderboard()
        {
        }

        private void quit()
        {
            EventQueue.publish(new RequestApplicationQuitEvent {});
        }
    }
}