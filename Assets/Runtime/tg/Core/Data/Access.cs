
namespace tg.data
{

    public static class access
    {
        public static readonly DAO<game.events.GameOverEvent.Reason>    GAME_GAMEOVER                      = new DAO<game.events.GameOverEvent.Reason>("game.gameover", DataDomain.App, DataStorage.Memory, DataScope.Game, DataVersioning.Ignore);
        public static readonly DAO<level.generator.GeneratorSettings>   LEVEL_ACTIVE_GENERATOR_SETTINGS    = new DAO<level.generator.GeneratorSettings>("level.active_generator_settings", DataDomain.App, DataStorage.Memory, DataScope.Session, DataVersioning.Ignore);
    }
}
