namespace FeatBit.Sdk.Server.Events
{
    internal interface IEventBuffer
    {
        void AddEvent(IEvent @event);

        bool IsEmpty { get; }

        void Clear();

        IEvent[] EventsSnapshot { get; }
    }
}