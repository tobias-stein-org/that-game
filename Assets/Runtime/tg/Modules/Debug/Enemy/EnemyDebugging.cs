using System.Collections;

using UnityEngine;
using Unity.Entities;

namespace tg.debug
{
    using tg.ai;
    using tg.ai.behaviour;
    using tg.enemy;
    using static tg.level.generator.Generator;

    public class EnemyDebugging : MonoBehaviour
    {
        public Enemy                    self;

        private Coroutine               fetch;

        public Sensor                   sensor { get; private set; } = Sensor.Default;

        private BehaviourContextData[]  behaviourContextDataArray = new BehaviourContextData[BehaviourContextInternal.MAX_BEHAVIOURS];

        public unsafe BehaviourSolver.BehaviourContextDataInternal     behaviourContextData
        {
            get
            {
                var context = BehaviourContextData.Default;
                switch(this.contextBehaviourDetails)
                {
                    case ContextBehaviourDetails.Wander:    { context = this.behaviourContextDataArray[IBehaviourContext<Wander>.ID]; break; }
                    case ContextBehaviourDetails.Flee:      { context = this.behaviourContextDataArray[IBehaviourContext<Flee>.ID]; break; }
                    case ContextBehaviourDetails.Avoid:     { context = this.behaviourContextDataArray[IBehaviourContext<Avoid>.ID]; break; }
                    case ContextBehaviourDetails.Pursue:    { context = this.behaviourContextDataArray[IBehaviourContext<Pursue>.ID]; break; }
                    case ContextBehaviourDetails.Solved:    { context = this.behaviourContextDataArray[IBehaviourContext<Solved>.ID]; break; }
                }

                return *(BehaviourSolver.BehaviourContextDataInternal*)&context;
            }
        }

        [System.Flags]
        public enum SensorDetails
        {
            Hide            = 0,
            ShowRange       = 1,
            ShowOutput      = 2,
            ShowDistance    = 4,
            ShowTargetTag   = 8,
        }

        public enum ContextBehaviourDetails
        {
            Hide,
            Wander,
            Flee,
            Avoid,
            Pursue,

            Solved
        }

        public SensorDetails            sensorDetails           = SensorDetails.ShowRange | SensorDetails.ShowOutput | SensorDetails.ShowDistance | SensorDetails.ShowTargetTag;
        public ContextBehaviourDetails  contextBehaviourDetails = ContextBehaviourDetails.Hide;

        public void Start()     { this.fetch = this.StartCoroutine(this.fetchSensorData(World.DefaultGameObjectInjectionWorld.EntityManager, this.self)); }
        public void OnDestroy() { this.StopCoroutine(this.fetch); }

        private IEnumerator fetchSensorData(EntityManager entityManager, Enemy self)
        {
            while(true)
            {
                yield return new WaitForSeconds(0.15f);
                yield return new WaitForEndOfFrame();

                if((this.sensorDetails & SensorDetails.Hide) == 0)
                {
                    this.sensor = entityManager.GetComponentData<Sensor>(self.entity);
                }

                if(this.contextBehaviourDetails != ContextBehaviourDetails.Hide)
                {
                    var buffer = entityManager.GetBuffer<BehaviourContextData>(self.entity, true);
                    for(int i = 0; i < buffer.Length; i++) { this.behaviourContextDataArray[i] = buffer[i]; }
                }
            }
        }
    }
}
