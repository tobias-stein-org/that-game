using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace tg.events
{
    /// <summary>
    /// Syntactic sugar to alias a plain int type to a more meaningful type name.
    /// </summary>
    public struct EventType : IEquatable<EventType>
    {
        private int typeId;

        public static implicit operator EventType(Type EventT)  { return new EventType { typeId = EventT.FullName.GetHashCode() }; }

        public bool Equals(EventType other) { return this.typeId.Equals(other.typeId); }
    }


    public interface IEvent
    {
        public EventType    type { get { return this.GetType(); } }
        public string       Name { get { return this.GetType().Name; } }
    }

    internal static class Events
    {
        /// <summary>
        /// A list of all available event types.
        /// </summary>
        public static readonly List<Type> registry = new List<Type>();

        static Events()
        {
            Type TEvent = typeof(IEvent);

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
                registry.AddRange(TEvents);
            }

            // sort all events in lexicographic order A-Z
            registry.Sort((a, b) => { return a.Name.CompareTo(b.Name); });
        }
    }
}
