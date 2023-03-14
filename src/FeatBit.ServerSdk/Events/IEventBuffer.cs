namespace FeatBit.Sdk.Server.Events
{
    internal interface IEventBuffer
    {
        bool AddEvent(IEvent @event);

        bool IsEmpty { get; }

        void Clear();

        IEvent[] EventsSnapshot { get; }
    }
}