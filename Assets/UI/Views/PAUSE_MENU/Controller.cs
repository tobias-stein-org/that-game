using tg.application.events;
using tg.events;
using tg.game.events;
using tg.ui.menu;
using UnityEngine.UIElements;

namespace tg.ui.view
{
    public class PAUSE_MENUController : IViewController
    {
        public void activated(VisualElement view)
        {
            view.Q<Button>("resume").clicked        += this.resume;
            view.Q<Button>("settings").clicked      += this.settings;
            view.Q<Button>("quit").clicked          += this.quit;
        }

        public void deactivated(VisualElement view)
        {
            view.Q<Button>("resume").clicked        -= this.resume;
            view.Q<Button>("settings").clicked      -= this.settings;
            view.Q<Button>("quit").clicked          -= this.quit;
        }

        private void resume()
        {
            Menu.close();
        }

        private void settings()
        {
            Menu.show(menus.SETTINGS);
        }

        private void quit()
        {
            Menu.showDialog(
                "Quit Game",
                "Do you really want to quit?",
                ("Yes", () =>
                {
                    EventQueue.publish(new GameOverEvent { reason = GameOverEvent.Reason.PLAYER_QUIT });
                    Loading.transition();
                }),
                ("No", () => { })
            );
        }
    }
}