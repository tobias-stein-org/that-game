using UnityEngine;

namespace tg.test.level
{
    using tg.events;
    using tg.level.events;

    public class LevelTester : MonoBehaviour
    {
        // Start is called before the first frame update
        void Start()
        {
            EventQueue.publish(new RequestNewLevelEvent {});
        }
    }
}
