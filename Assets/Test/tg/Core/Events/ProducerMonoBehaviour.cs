using UnityEngine;
using Unity.Entities;

namespace tg.test.events
{
    using tg.events;

    public class ProducerMonoBehaviour : MonoBehaviour, IEventListener
    {
        public	int		totalProduced;
		public	int		sentEvents; 

        void LateUpdate()
        {
            var quantity = Random.Range(1, 5);

            EventQueue.publish(new ProduceEvent { produced = quantity });
            this.totalProduced += quantity;
            this.sentEvents++;
        }
    }
}
