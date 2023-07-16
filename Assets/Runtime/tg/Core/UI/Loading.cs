namespace tg.ui
{
    using tg.events;
    using events;

    public static class transition
    {
        private const string LOADING_SCREEN = "LOADING_SCREEN";

        public static void showLoadingScreen(string title = "Loading ...")
        {
            EventQueue.publish(new ShowLoadingScreenEvent { title = title });
            EventQueue.publish(new ShowViewEvent { name = LOADING_SCREEN });
        }

        public static void hideLoadingScreen()
        {
            EventQueue.publish(new HideLoadingScreenEvent {});
            EventQueue.publish(new HideViewEvent { name = LOADING_SCREEN });
        }
    }
}
