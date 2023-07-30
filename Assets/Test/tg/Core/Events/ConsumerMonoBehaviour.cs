using UnityEngine;
using Unity.Entities;

namespace tg.test.events
{
    using tg.events;

    public class ConsumerMonoBehaviour : MonoBehaviour, IEventListener
    {
        public	int		totalProduced;
		public	int		recvEvents; 

        void Start()
        {
            EventQueue.subscribe(this);
        }

        void OnDestroy()
        {
            EventQueue.unsubscribe(this);
        }

        private void onProduceEvent(ProduceEvent e)
        {
            this.totalProduced += e.produced;
            this.recvEvents++;
        }
    }
}
