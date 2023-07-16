using UnityEngine.UIElements;

namespace tg.ui.view
{
    using tg.events;
    using tg.ui.events;


    public class LOADING_SCREENController : IViewController, IEventListener<LOADING_SCREENController>
    {
        private ProgressBar progress;
        private Label       step;

        public void activated(VisualElement view)
        {
            this.progress       = view.Q<ProgressBar>("progress");
            this.step           = view.Q<Label>("step");

            EventQueue.subscribe(this);
        }

        public void deactivated(VisualElement view)
        {
            EventQueue.unsubscribe(this);
        }

        void onUpdateLoadingScreenProgressEvent(UpdateLoadingScreenProgressEvent e)
        {
            this.progress.style.visibility  = Visibility.Visible;
            this.step.text                  = e.step ?? "";

            // we do not set the progress directly to prevent jumpy loading screens, instead we schedule a
            // callback to gradually update the progress over time to the current set loading progress value.
            progress.schedule
                .Execute((TimerState state) =>
                {
                    this.progress.value    += state.deltaTime * 1e-3f;
                    this.progress.title     = $"{this.progress.value:P2}";

                    // auto hide loading screen, when 100% is reached.
                    if(this.progress.value >= 1f)
                    {
                        ui.transition.hideLoadingScreen();
                    }
                })
                .Until(() => this.progress.value >= e.progress);
        }
    }
}