using tg.events;
using tg.game.events;
using tg.ui.menu;
using UnityEngine.UIElements;

namespace tg.ui.view
{
    public class GAME_OVERController : IViewController
    {
        public void activated(VisualElement view)
        {
            var resume                          = view.Q<Button>("resume");

            switch (data.access.GAME_GAMEOVER.read())
            {
                case GameOverEvent.Reason.PLAYER_DONE:
                    resume.text                 = "Next";
                    break;
                case GameOverEvent.Reason.PLAYER_LOST:
                    resume.text                 = "Try again";
                    break;
            }

            resume.clicked                      += this.resume;
            view.Q<Button>("quit").clicked      += this.quit;
        }

        public void deactivated(VisualElement view)
        {
            view.Q<Button>("resume").clicked    -= this.resume;
            view.Q<Button>("quit").clicked      -= this.quit;
        }

        private void resume()
        {
            EventQueue.publish(new StartGameEvent { });
            Menu.close();
        }

        private void quit()
        {
            Menu.showDialog(
                "Quit Game",
                "Do you really want to quit?",
                ("Yes", () =>
                {
                    Menu.show(menus.MAIN_MENU, false);
                }),
                ("No", () => { })
            );
        }
    }
}