using Unity.Collections;
using Unity.Entities;

namespace tg.test.events
{
    using tg.events;

    public partial struct ProducerSystem : ISystem
    {
        public	int		totalProduced;
		public	int		sentEvents;

        public void OnCreate(ref SystemState state)
		{
			var system = state.WorldUnmanaged.GetUnsafeSystemRef<ProducerSystem>(state.SystemHandle);
			system.totalProduced	= 0;
			system.sentEvents		= 0;
		}

		public void OnDestroy(ref SystemState state)
		{
		}

		public void OnUpdate(ref SystemState state)
		{
			ref var system = ref state.WorldUnmanaged.GetUnsafeSystemRef<ProducerSystem>(state.SystemHandle);

			int	quantity = 3;

			EventQueue.publish(new ProduceEvent { produced = quantity });

			system.totalProduced += quantity;
			system.sentEvents++;
		}
    }
}
