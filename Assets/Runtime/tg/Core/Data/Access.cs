
namespace tg.data
{

    public static class access
    {
        public static readonly DAO<game.events.GameOverEvent.Reason> GAME_GAMEOVER = new DAO<game.events.GameOverEvent.Reason>("game.gameover", DataDomain.App, DataStorage.Memory, DataScope.Game, DataVersioning.Ignore);
    }
}
