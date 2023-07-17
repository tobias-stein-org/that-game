using UnityEngine.UIElements;

using Unity.Entities;

namespace tg.ui
{
    using tg.events;
    using events;

    [CreateBefore(typeof(EventQueue))]
    public partial class Loading : SystemBase, IEventListener<Loading>
    {
        private const string            LOADING_SCREEN  = "LOADING_SCREEN";

        private static VisualElement    loadingScreen   = null;

        private static Label            title           = null;
        private static ProgressBar      progress        = null;
        private static Label            step            = null;

        protected override void OnCreate()
        {
            base.OnCreate();

        }

        protected override void OnUpdate()
        {
            EventQueue.subscribe(this);
            EventQueue.publish(new SpawnViewEvent { name = Loading.LOADING_SCREEN });
        }

        void onViewSpawnEvent(ViewSpawnEvent e)
        {
            if(e.view.name == Loading.LOADING_SCREEN)
            {
                Loading.loadingScreen = e.view;

                Loading.title           = e.view.Q<Label>("title");
                Loading.progress        = e.view.Q<ProgressBar>("progress");
                Loading.step            = e.view.Q<Label>("step");
            }
        }

        public static void showLoadingScreen(string title = "Loading ...")
        {
            Unity.Assertions.Assert.IsNotNull(Loading.loadingScreen, "Loading screen resource is not initialized!");

            Loading.title.text                  = title ?? "Loading ...";
            Loading.progress.style.visibility   = Visibility.Hidden;

            EventQueue.publish(new ShowLoadingScreenEvent { });
            EventQueue.publish(new ShowViewEvent { name = LOADING_SCREEN });
        }

        public static void updateLoadingProgress(string step, float progress)
        {
            Unity.Assertions.Assert.IsNotNull(Loading.loadingScreen, "Loading screen resource is not initialized!");

            Loading.progress.style.visibility   = Visibility.Visible;
            Loading.step.text                   = step ?? "";

            // we do not set the progress directly to prevent jumpy loading screens, instead we schedule a
            // callback to gradually update the progress over time to the current set loading progress value.
            Loading.progress.schedule
                .Execute((TimerState state) =>
                {
                    Loading.progress.value += state.deltaTime * 1e-3f;
                    Loading.progress.title = $"{Loading.progress.value:P2}";

                    // auto hide loading screen, when 100% is reached.
                    if(Loading.progress.value >= 1f)
                    {
                        ui.Loading.hideLoadingScreen();
                    }
                })
                .Until(() => Loading.progress.value >= progress);
        }

        public static void hideLoadingScreen()
        {
            EventQueue.publish(new HideLoadingScreenEvent { });
            EventQueue.publish(new HideViewEvent { name = LOADING_SCREEN });
        }
    }
}
