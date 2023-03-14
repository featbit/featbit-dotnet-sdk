namespace FeatBit.Sdk.Server.Events;

internal sealed class IntEvent : PayloadEvent
{
    public int Value { get; }

    public IntEvent(int value)
    {
        Value = value;
    }
}