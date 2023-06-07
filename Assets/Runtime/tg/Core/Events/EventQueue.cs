using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEngine;

namespace tg.events
{
    /// <summary>
    /// A global accessable EventQueue. IEventListener types can subscribe and unsubscribe to this event queue.
    /// Subscribed listener will automatically receive events for which they implemented a handler method for.
    /// </summary>
    [UpdateInGroup(typeof(InitializationSystemGroup), OrderFirst = true)]
    public partial class EventQueue : SystemBase
    {
        private static EventQueue                                   Instance        = null;

        public bool                                                 isDisposed      { get; private set; } = false;

        private readonly HashSet<int>                               subscriber      = new HashSet<int>();
        private readonly Dictionary<EventType, List<EventHandler>>  eventHandler    = new Dictionary<EventType, List<EventHandler>>();
        private readonly ConcurrentQueue<IEvent>                    eventQueue      = new ConcurrentQueue<IEvent>();

        /// <summary>
        /// Helper structure to hold the event listener instance and the event handler method.
        /// </summary>
        private struct EventHandler 
        {
            /// <summary>
            /// Instance of the event listener.
            /// </summary>
            public IEventListener instance;

            /// <summary>
            /// The handler method of that instance.
            /// </summary>
            public RuntimeMethodHandle handler;
        }

        /// <summary>
        /// Set signleton instance.
        /// </summary>
        protected override void OnCreate()
        {
            EventQueue.Instance = this;
        }

        protected override void OnUpdate()
        {
            /**
                Make a copy of the current event queue and clear the original queue.
                We do this, because event listeners might put new events in the queue
                as an reaction to a received event. These new events will be processed
                next frame.
            */

            Queue<IEvent> ThisFrameEvents = new Queue<IEvent>();
            while (Instance.eventQueue.TryDequeue(out IEvent ev)) { ThisFrameEvents.Enqueue(ev); }

            // Process original event queue
            foreach (IEvent e in ThisFrameEvents)
            {
                EventType EventT = e.type;

                // simple log of the fired event
                Debug.Log($"Fire event '{e.Name}': {JsonUtility.ToJson(e)}");

                if(this.eventHandler.TryGetValue(EventT, out List<EventHandler> Handlers))
                {
                    // dispatch evnet to all listeners
                    foreach (EventHandler handler in Handlers) { MethodBase.GetMethodFromHandle(handler.handler).Invoke(handler.instance, new object[] { e }); }
                }
            }
        }

        /// <summary>
        /// Dispose this event queue. This will prevent any further event publishs and subscribs.
        /// </summary>
        protected override void OnDestroy()
        {
             // clear any pending events
            this.eventQueue.Clear();

            // mark this event queue as disposed.
            this.isDisposed = true;
        }

        #region Global EventQueue Methods

        public static void subscribe<T>(T listener)
            where T : IEventListener<T>
        {
            Type    TListener   = listener.GetType();
            int     instnaceId  = listener.GetHashCode();

            UnityEngine.Assertions.Assert.IsFalse(EventQueue.Instance.isDisposed, $"Cannot subscribe event listener {TListener.FullName} instance [{instnaceId}]. EventQueue has been already disposed!");

            if(Instance.subscriber.Contains(instnaceId))
            {
                //Debug.Log($"Event listener {TListener.FullName} instance [{instnaceId}] already subscribed.");
                return;
            }

            var EventHandlers = Listener.registry[IEventListener<T>.ListenerID];
            if(EventHandlers.Count == 0)
            {
                Debug.LogWarning($"EventListener {TListener.FullName} subscribed, but has no event handlers defined.");
            }
            else
            {
                // Insert this event listeners handler method into the look-up map
                foreach(var (eventType, handler) in EventHandlers)
                {
                    // get handler list for this event type
                    if (!Instance.eventHandler.TryGetValue(eventType, out List<EventHandler> Handlers))
                    {
                        // if list does not exist yet, create it
                        Handlers = new List<EventHandler>();
                        Instance.eventHandler.Add(eventType, Handlers);

                    }

                    // add new entry for event handler
                    Handlers.Add(new EventHandler { instance = listener, handler = handler });
                }
            }


            Instance.subscriber.Add(instnaceId);
            Debug.Log($"EventListener '{TListener.FullName}' instance [{instnaceId}] subscribed.");
        }

        public static void unsubscribe<T>(T listener)
            where T : IEventListener<T>
        {
            Type    TListener   = listener.GetType();
            int     instnaceId  = listener.GetHashCode();

            if(Instance.subscriber.Contains(instnaceId))
            {

                var EventHandlers = Listener.registry[IEventListener<T>.ListenerID];

                // remove all listeners event handlers
                foreach(var Handlers in Instance.eventHandler.Values) { Handlers.RemoveAll(x => x.instance.GetHashCode() == listener.GetHashCode()); }

                Instance.subscriber.Remove(instnaceId);
                Debug.Log($"EventListener '{TListener.FullName}' instance [{instnaceId}] unsubscribed.");

            }
        }

        /// <summary>
        /// Enques a new event to the queue.
        /// </summary>
        /// <param name="Event"></param>
        /// <param name="Source"></param>
        /// <typeparam name="TEvent"></typeparam>
        public static void publish<TEvent>(TEvent ev)
            where TEvent : notnull, IEvent
        {
            UnityEngine.Assertions.Assert.IsFalse(EventQueue.Instance.isDisposed, $"Cannot publish new event {ev.Name}. EventQueue has been already disposed!");

            // enqueue new event
            Instance.eventQueue.Enqueue(ev); 
        }

        #endregion
    }
}
