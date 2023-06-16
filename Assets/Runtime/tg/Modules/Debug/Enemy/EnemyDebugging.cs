using System.Collections;

using UnityEngine;
using Unity.Entities;

namespace tg.debug
{
    using tg.ai;
    using tg.enemy;

    public class EnemyDebugging : MonoBehaviour
    {
        public Enemy            self;

        public Sensor           sensor { get; private set; }

        private Coroutine       fetch;

        public void Start()
        {
            this.fetch = this.StartCoroutine(this.fetchSensorData(World.DefaultGameObjectInjectionWorld.EntityManager, this.self));
        }

        public void OnDestroy()
        {
            this.StopCoroutine(this.fetch);
        }

        private IEnumerator fetchSensorData(EntityManager entityManager, Enemy self)
        {
            while(true)
            {
                yield return new WaitForSeconds(0.1f);
                yield return new WaitForEndOfFrame();

                this.sensor = entityManager.GetComponentData<Sensor>(self.entity);
            }
        }
    }
}
