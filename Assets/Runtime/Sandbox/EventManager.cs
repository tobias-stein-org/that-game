using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

public static class TypeEx
{
    /// <summary>
    /// Helper method to get all public and private methods of a class type (including parent classes).
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<MethodInfo> GetAllMethodsInClassHierachy(this Type InType, Type InRootParentClass = null)
    {
        // grab all methods
        IEnumerable<MethodInfo> methods = InType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        // do it recursevly for parent classes
        if (InType.BaseType != InRootParentClass) { methods = methods.Concat(InType.BaseType.GetAllMethodsInClassHierachy(InRootParentClass)); }
        // return all found methods
        return methods;
    }
}

/// <summary>
/// Syntactic sugar to alias a plain int type to a more meaningful type name.
/// </summary>
public struct EventType
{
    private int _EventType;

    public static implicit operator EventType(int EventT) { return new EventType { _EventType = EventT }; }
    public static implicit operator int(EventType EventT) { return EventT._EventType; }

    public override string ToString() { return this._EventType.ToString(); }
}


public interface IEvent
{
    /// <summary>
    /// Returns a unique event type identifier based on the type of the provided object.
    /// </summary>
    /// <returns></returns>
    public static EventType GetEventType(object EventT) { return EventT.GetType().FullName.GetHashCode(); }
    
    /// <summary>
    /// Retruns a unique event type based on the provided type.
    /// </summary>
    /// <returns></returns>
    public static EventType GetEventType(Type EventT) { return EventT.FullName.GetHashCode(); }

        /// <summary>
    /// Returns this event name.
    /// </summary>
    /// <returns></returns>
    public string Name { get { return this.GetType().Name; } }
}

/// <summary>
/// Marker interface for automatically event lister collection.
/// </summary>
public interface IEventListener { }


/// <summary>
/// Contains the main message pump to dispatch all accumulated events to 
/// event listeners.
/// </summary>
public class EventManager : MonoBehaviour
{
    /// <summary>
    /// List of all defined events.
    /// </summary>
    public static readonly List<Type> EventTypes = new List<Type>();

    /// <summary>
    /// Look-up cache for event handler methods of every declared IEventListener type.
    /// </summary>
    /// <returns></returns>
    private static readonly Dictionary<Type, List<MethodInfo>> EventHandlerCache = new Dictionary<Type, List<MethodInfo>>();

    public static EventManager Instance;

    //class Baker : Baker<EventManager>
    //{
    //    public override void Bake(EventManager authoring)
    //    {
    //        EventManager.Instance = authoring;
    //    }
    //}

    public void Awake()
    {
        EventManager.Instance = this;
    }

    public void LateUpdate()
    {
        this.ProcessEventQueue();
    }


    /// <summary>
    /// Static constructor. Is at most executed once.
    /// We will use it to gather all defined events.
    /// </summary>
    static EventManager()
    {
        Type TEvent = typeof(IEvent);
        Type TIEventListener = typeof(IEventListener);

        // Determine all defined event types
        {
            // Get event types from all currently referenced assemblies.
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // get all defined event types in this assembly
                List<Type> TEvents = asm
                    // get all types from this assembly
                    .GetTypes()
                    // filter only sub-classes of Event type
                    .Where(T => TEvent.IsAssignableFrom(T) && T != TEvent)
                    .ToList();

                // append found event types in the current assembly
                EventTypes.AddRange(TEvents);
            }

            // sort all events in lexicographic order A-Z
            EventTypes.Sort( (a, b) => { return a.Name.CompareTo(b.Name); });
        }

        // Determine all defined IEventListener types and cache their handlers
        {
            List<Type> EventListenerTypes = new List<Type>();

            // Get event listener types from all currently referenced assemblies.
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // get all defined event listener types in this assembly
                List<Type> EventListeners = asm
                    .GetTypes()
                    // filter only sub-classes of IEventListener
                    .Where(T => TIEventListener.IsAssignableFrom(T) && T.GetInterface("IEventListener") != null)
                    .ToList();

                // append found event types in the current assembly
                EventListenerTypes.AddRange(EventListeners);
            }


            foreach(Type ThisClass in EventListenerTypes)
            {
                // Make sure we perform this process only once.
                if(EventHandlerCache.ContainsKey(ThisClass)) { return; }

                // Collect all event handlers
                List<MethodInfo> EventHandlers = ThisClass.GetAllMethodsInClassHierachy()
                    // get all class methods of the following signature 'void (Event)'
                    .Where(x => 
                        // make sure method is not generic
                        (!x.IsGenericMethod) &&
                        // return type is void
                        (x.ReturnParameter.ParameterType == typeof(void)) && 
                        // has excatly one input parameter, which is a sub class of type Event, but not Event
                        (x.GetParameters().Length == 1) && (x.GetParameters()[0].ParameterType != TEvent) && TEvent.IsAssignableFrom(x.GetParameters()[0].ParameterType)
                    )
                    .ToList();


                // Sanity check, if there are multiple event handlers handling the same event type.
                var DuplicatedEventHandlers = EventHandlers
                    .GroupBy(handler => handler.GetParameters()[0].ParameterType)
                    .Where(group => group.Count() > 1)
                    .Select(group => new { EventType = group.Key, Handler = group.Select(z => z.Name) })
                    .ToList();

                foreach (var duplicates in DuplicatedEventHandlers) { Debug.LogError($"{duplicates.EventType.Name} event is handled by multiple handlers: {String.Join(", ", duplicates.Handler)}"); }
                Debug.Assert(DuplicatedEventHandlers.Count == 0, $"{ThisClass.FullName} event listener has multiple event handler for the same event type. There can only by one event handler method per event type!");
            
                // store result in cache
                EventHandlerCache.Add(ThisClass, EventHandlers);
            }
        }
    }


    /// <summary>
    /// Queue of all accumulated events during this frame.
    /// </summary>
    /// <typeparam name="Event"></typeparam>
    /// <returns></returns>
    private readonly ConcurrentQueue<IEvent> EventQueue = new ConcurrentQueue<IEvent>();

    /// <summary>
    /// Helper structure to hold the event listener instance and the event handler method.
    /// </summary>
    private struct EventHandler 
    {
        /// <summary>
        /// Instance of the event listener.
        /// </summary>
        public IEventListener EventListener;

        /// <summary>
        /// The handler method of that instance.
        /// </summary>
        public MethodInfo Handler;
    }

    /// <summary>
    /// Mapping from event type to event listeners handler method.
    /// </summary>
    /// <returns></returns>
    private readonly Dictionary<EventType, List<EventHandler>> EventHandlers = new Dictionary<EventType, List<EventHandler>>();

    /// <summary>
    /// Register an event listener and dispatch incoming events to its handlers.
    /// </summary>
    /// <param name="Listener"></param>
    public void Subscribe(IEventListener Listener) 
    {
        Type TListener = Listener.GetType();

        List<MethodInfo> EventHandlers = EventHandlerCache[TListener];
        if(EventHandlers.Count == 0)
        {
            Debug.LogWarning($"EventListener {TListener.FullName} subscribed, but has no event handlers defined.");
            return;
        }

        // Insert this event listeners handler method into the look-up map
        foreach(MethodInfo handler in EventHandlers)
        {
            ParameterInfo EventPI = handler.GetParameters()[0];

            // determine event type from listener handlers methods
            EventType EventT = IEvent.GetEventType(EventPI.ParameterType);

            // get handler list for this event type
            if (!this.EventHandlers.TryGetValue(EventT, out List<EventHandler> Handlers))
            {
                // if list does not exist yet, create it
                Handlers = new List<EventHandler>();
                this.EventHandlers.Add(EventT, Handlers);

                Debug.Log($"EventListener '{TListener.FullName}' subscribed to '{EventPI.ParameterType.FullName}' event.");
            }

            // Sanity check, make sure listener does not have already a handler registered
            if (Handlers.Count(x => x.EventListener == Listener) > 0)
            {
                Debug.LogWarning($"Event listener {TListener.FullName} already has handler registered for event {EventPI.ParameterType.Name}. Skip registering this handler.");
                continue;
            }

            // add new entry for event handler
            Handlers.Add(new EventHandler { EventListener = Listener, Handler = handler });
        }
    }

    /// <summary>
    /// Remove an event listener from event queue so it will no longer receive events.
    /// </summary>
    /// <param name="Listener"></param>
    public void Unsubscribe(IEventListener Listener) 
    {
        Type TListener = Listener.GetType();

        Debug.Log($"Unsubscribe event listener '{TListener.FullName}'");

        List<MethodInfo> EventHandlers = EventHandlerCache[TListener];

        // If no handlers, nothing todo.
        if(EventHandlers.Count == 0) { return; }

        // remove all listeners event handlers
        foreach(var Handlers in this.EventHandlers.Values) { Handlers.RemoveAll(x => x.EventListener == Listener); }
    }

    /// <summary>
    /// Enques a new event to the queue.
    /// </summary>
    /// <param name="Event"></param>
    /// <param name="Source"></param>
    /// <typeparam name="TEvent"></typeparam>
    public void Publish<TEvent>(TEvent Event) where TEvent : IEvent 
    {
        // enqueue new event
        this.EventQueue.Enqueue(Event); 
    }

    /// <summary>
    /// Process all events currently in the queue.
    /// </summary>
    public void ProcessEventQueue()
    {
        /**
            Make a copy of the current event queue and clear the original queue.
            We do this, because event listeners might put new events in the queue
            as an reaction to a received event. These new events will be processed
            next frame.
        */

        Queue<IEvent> ThisFrameEvents = new Queue<IEvent>();
        while (this.EventQueue.TryDequeue(out IEvent ev)) { ThisFrameEvents.Enqueue(ev); }

        // Process original event queue
        foreach (IEvent e in ThisFrameEvents) { this.ProcessEvent(e); }
    }

    /// <summary>
    /// Process a single event.
    /// </summary>
    /// <param name="e"></param>
    private void ProcessEvent(IEvent e)
    {
        EventType EventT = IEvent.GetEventType(e);

        // simple log of the fired event
        Debug.Log($"Fire event '{e.Name}': {JsonUtility.ToJson(e)}");

        if(this.EventHandlers.TryGetValue(EventT, out List<EventHandler> Handlers))
        {
            // dispatch evnet to all listeners
            foreach (EventHandler handler in Handlers) { handler.Handler.Invoke(handler.EventListener, new object[] { e }); }
        }
    }
}