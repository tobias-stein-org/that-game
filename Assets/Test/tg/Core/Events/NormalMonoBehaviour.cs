using System.Collections;
using System.Collections.Generic;
using tg.test.events;
using Unity.Entities;
using UnityEngine;

namespace tg.events
{
    public struct NormalEvent : IEvent {}

    public class NormalMonoBehaviour : MonoBehaviour, IEventListener<NormalMonoBehaviour>
    {
        // Start is called before the first frame update
        void Start()
        {
            EventQueue.subscribe(this);
        }

        void OnDestroy()
        {
            EventQueue.unsubscribe(this);
        }

        // Update is called once per frame
        void Update()
        {
            EventQueue.publish(new NormalEvent {});
        }

        private void OnIncEvent(IncEvent e)
        {
            Debug.Log($"NormalMonoBehaviour.OnIncEvent [{this.GetHashCode()}]");
        }

        private void OnNormalEvent(NormalEvent e)
        {
            Debug.Log($"NormalMonoBehaviour.OnNormalEvent [{this.GetHashCode()}]");
        }
    }
}
