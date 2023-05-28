using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;

namespace tg.test.events
{
    using tg.events;

    public class TestEventQueue
    {
        private World       testWorld;
        private GameObject  testGO;


        [UnityTest]
        public IEnumerator SendRecv_Between_Managed_Unmanaged_System()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer and producer system
            var simulationSystemGroup = this.testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            {
                simulationSystemGroup.AddSystemToUpdateList(this.testWorld.CreateSystem<ConsumerSystem>());
                yield return null;

                simulationSystemGroup.AddSystemToUpdateList(this.testWorld.CreateSystem<ProducerSystem>());
            }

            // let it run for a few frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            var consumer = this.testWorld.GetExistingSystemManaged<ConsumerSystem>();

            var producerHandle = this.testWorld.GetExistingSystem<ProducerSystem>();
            var producer = this.testWorld.Unmanaged.GetUnsafeSystemRef<ProducerSystem>(producerHandle);

            Assert.AreEqual(producer.sentEvents, consumer.recvEvents);
            Assert.AreEqual(producer.totalProduced, consumer.totalProduced);
        }

        [UnityTest]
        public IEnumerator SendRecv_Between_MonoBehaviour()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer and producer MonoBehaviour
            var consumer = this.testGO.AddComponent<ConsumerMonoBehaviour>();
            yield return null;

            var producer = this.testGO.AddComponent<ProducerMonoBehaviour>();

            // let it run for a few frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(producer.sentEvents, consumer.recvEvents);
            Assert.AreEqual(producer.totalProduced, consumer.totalProduced);
        }

        [UnityTest]
        public IEnumerator SendRecv_Between_MonoBehaviour_System()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer system and producer MonoBehaviour
            var simulationSystemGroup = this.testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            {
                simulationSystemGroup.AddSystemToUpdateList(this.testWorld.CreateSystem<ConsumerSystem>());
                yield return null;
            }
            var producer = this.testGO.AddComponent<ProducerMonoBehaviour>();

            // let it run for a few frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            var consumer = this.testWorld.GetExistingSystemManaged<ConsumerSystem>();

            Assert.AreEqual(producer.sentEvents, consumer.recvEvents);
            Assert.AreEqual(producer.totalProduced, consumer.totalProduced);
        }

        [UnityTest]
        public IEnumerator SendRecv_Between_System_MonoBehaviour()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer MonoBehaviour and producer system
            
            var consumer = this.testGO.AddComponent<ConsumerMonoBehaviour>();

            var simulationSystemGroup = this.testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            {
                simulationSystemGroup.AddSystemToUpdateList(this.testWorld.CreateSystem<ProducerSystem>());
                yield return null;
            }

            // let it run for a few frames
            yield return null;
            yield return null;
            yield return null;
            yield return null;
            yield return null;

            var producerHandle = this.testWorld.GetExistingSystem<ProducerSystem>();
            var producer = this.testWorld.Unmanaged.GetUnsafeSystemRef<ProducerSystem>(producerHandle);

            Assert.AreEqual(producer.sentEvents, consumer.recvEvents);
            Assert.AreEqual(producer.totalProduced, consumer.totalProduced);
        }

        [UnityTest]
        public IEnumerator SendRecv_Between_Coroutine_MonoBehaviour()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer MonoBehaviour
            
            var consumer = this.testGO.AddComponent<ConsumerMonoBehaviour>();

            var sentEvents = 0;
            var totalProduced = 0;

            
            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            Assert.AreEqual(sentEvents, consumer.recvEvents);
            Assert.AreEqual(totalProduced, consumer.totalProduced);
        }

        [UnityTest]
        public IEnumerator SendRecv_Between_Coroutine_System()
        {
            // EventQueue system must be created already
            Assert.NotNull(this.testWorld.GetExistingSystemManaged<EventQueue>());

            // spawn consumer system
            var simulationSystemGroup = this.testWorld.GetOrCreateSystemManaged<SimulationSystemGroup>();
            {
                simulationSystemGroup.AddSystemToUpdateList(this.testWorld.CreateSystem<ConsumerSystem>());
                yield return null;
            }

            var sentEvents = 0;
            var totalProduced = 0;

            
            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            {
                var quantity = Random.Range(1, 5);
                EventQueue.publish(new ProduceEvent { produced = quantity });
                totalProduced += quantity;
                sentEvents++;
            }
            yield return null;

            var consumer = this.testWorld.GetExistingSystemManaged<ConsumerSystem>();
            Assert.AreEqual(sentEvents, consumer.recvEvents);
            Assert.AreEqual(totalProduced, consumer.totalProduced);
        }

        [SetUp]
        public void SetUp()
        {
            if(World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated) { World.DefaultGameObjectInjectionWorld.Dispose(); };

            this.testWorld  = DefaultWorldInitialization.Initialize("test-world");
            this.testGO     = new GameObject("test-go");

            var initializationSystemGroup = this.testWorld.CreateSystemManaged<InitializationSystemGroup>();
            {
                initializationSystemGroup.AddSystemToUpdateList(this.testWorld.GetOrCreateSystemManaged<EventQueue>());
            }
        }

        [TearDown]
        public void TearDown()
        {
            GameObject.Destroy(this.testGO);

            this.testWorld.Dispose();
            this.testWorld = null;
        }
    }
}
