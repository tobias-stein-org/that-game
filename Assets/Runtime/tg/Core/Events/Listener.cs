
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace tg.events
{
    using tg.util;

    /// <summary>
    /// Syntactic sugar to alias a plain int type to a more meaningful type name.
    /// </summary>
    public struct ListenerType : IEquatable<ListenerType>
    {
        private int typeId;

        public static implicit operator ListenerType(Type TListener)  { return new ListenerType { typeId = TListener.FullName.GetHashCode() }; }

        public bool Equals(ListenerType other) { return this.typeId.Equals(other.typeId); }
    }

    /// <summary>
    /// Marker interface for automatically event lister collection.
    /// </summary>
    public interface IEventListener { }

    /// <summary>
    /// Static helper class for type look-up.
    /// </summary>
    internal static class Listener
    {
        /// <summary>
        /// Contains information about all available event listener types and their handler methods.
        /// </summary>
        public static readonly Dictionary<ListenerType, Dictionary<EventType, RuntimeMethodHandle>> registry = new Dictionary<ListenerType, Dictionary<EventType, RuntimeMethodHandle>>();

        static Listener()
        {
            Type        TEvent              = typeof(IEvent);
            Type        TIEventListener     = typeof(IEventListener);

            List<Type>  EventListenerTypes  = new List<Type>();

            // Get event listener types from all currently referenced assemblies.
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                // get all defined event listener types in this assembly
                List<Type> EventListeners = asm
                    .GetTypes()
                    // filter only sub-classes of IEventListener
                    .Where(T => TIEventListener.IsAssignableFrom(T) && T != TIEventListener)
                    .ToList();

                // append found event types in the current assembly
                EventListenerTypes.AddRange(EventListeners);
            }


            foreach(Type ThisClass in EventListenerTypes)
            {
                ListenerType TListenerID = ThisClass;

                // Make sure we perform this process only once.
                if(Listener.registry.ContainsKey(TListenerID)) { return; }

                // Collect all event handlers
                List<MethodInfo> EventHandlers = ThisClass.getAllMethodsInClassHierachy()
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

                foreach (var duplicates in DuplicatedEventHandlers) { UnityEngine.Debug.LogError($"{duplicates.EventType.Name} event is handled by multiple handlers: {String.Join(", ", duplicates.Handler)}. Ony one handler per event type is allowed."); }
                UnityEngine.Debug.Assert(DuplicatedEventHandlers.Count == 0, $"{ThisClass.FullName} event listener has multiple event handler for the same event type. There can only by one event handler method per event type!");
            
                // store result in cache
                Listener.registry.Add(TListenerID, EventHandlers.ToDictionary(mi => (EventType)mi.GetParameters()[0].ParameterType, mi => mi.MethodHandle));
            }
        }
    }
}
