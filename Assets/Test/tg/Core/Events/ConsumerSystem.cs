using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace tg.test.events
{
    using tg.events;

    [CreateBefore(typeof(ProducerSystem))]
    public partial class ConsumerSystem : SystemBase, IEventListener<ConsumerSystem>
    {
        public int totalProduced;
		public int recvEvents;

        protected override void OnStartRunning()
        {
            EventQueue.subscribe(this);
        }

        protected override void OnStopRunning()
        {
            EventQueue.unsubscribe(this);
        }

        protected override void OnUpdate()
        {
            
        }

        private void onProduceEvent(ProduceEvent e)
        {
            this.totalProduced += e.produced;
            this.recvEvents++;
        }
    }
}
