using System;

namespace FeatBit.Sdk.Server.Events
{
    internal interface IEventSerializer
    {
        public byte[] Serialize(ReadOnlyMemory<IEvent> events);
    }
}