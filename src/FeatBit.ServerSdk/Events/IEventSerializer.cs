using System;

namespace FeatBit.Sdk.Server.Events
{
    internal interface IEventSerializer
    {
        public byte[] Serialize(IEvent @event);

        public byte[] Serialize(ReadOnlyMemory<IEvent> events);
    }
}