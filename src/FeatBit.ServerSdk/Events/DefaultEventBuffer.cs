using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal sealed class DefaultEventBuffer : IEventBuffer
    {
        private readonly int _capacity;
        private readonly List<IEvent> _events;
        private readonly ILogger _logger;

        public DefaultEventBuffer(int capacity, ILogger logger)
        {
            _capacity = capacity;
            _events = new List<IEvent>();
            _logger = logger;
        }

        public void AddEvent(IEvent @event)
        {
            if (_events.Count >= _capacity)
            {
                _logger.LogWarning(
                    "Exceeded event queue capacity, event will be dropped. Increase capacity to avoid dropping events."
                );
            }
            else
            {
                _events.Add(@event);
            }
        }

        public int Count => _events.Count;

        public bool IsEmpty => Count == 0;

        public void Clear() => _events.Clear();

        public IEvent[] EventsSnapshot => _events.ToArray();
    }
}