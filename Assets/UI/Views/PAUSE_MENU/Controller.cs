using UnityEngine.UIElements;

using Unity.Entities;
using Unity.Collections;

namespace tg.ui.view
{
    using tg.application.events;
    using tg.events;
    using tg.game.events;
    using tg.ui.menu;

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
                    var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
                    foreach(var player in entityManager.CreateEntityQuery(new EntityQueryDesc { All = new ComponentType[] { typeof(tg.player.entities.Player) } }).ToEntityArray(Allocator.Temp))
                    {
                        EventQueue.publish(new tg.combat.events.EntityDiedEvent { entity = player });
                    }

                    tg.data.access.GAME_GAMEOVER.upsert(GameOverEvent.Reason.PLAYER_QUIT);

                    EventQueue.publish(new GameOverEvent                        { reason = GameOverEvent.Reason.PLAYER_QUIT });
                    EventQueue.publish(new tg.enemy.events.KillAllEnemyEvent    { });
                    Loading.transition();
                }),
                ("No", () => { })
            );
        }
    }
}