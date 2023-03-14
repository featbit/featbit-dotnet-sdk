using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Events
{
    internal sealed class DefaultEventBuffer : IEventBuffer
    {
        private readonly int _capacity;
        private readonly List<IEvent> _events;

        public DefaultEventBuffer(int capacity)
        {
            _capacity = capacity;
            _events = new List<IEvent>();
        }

        public bool AddEvent(IEvent @event)
        {
            if (_events.Count >= _capacity)
            {
                return false;
            }

            _events.Add(@event);
            return true;
        }

        public int Count => _events.Count;

        public bool IsEmpty => Count == 0;

        public void Clear() => _events.Clear();

        public IEvent[] EventsSnapshot => _events.ToArray();
    }
}