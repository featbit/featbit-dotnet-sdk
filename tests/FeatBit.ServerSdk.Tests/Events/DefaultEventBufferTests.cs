namespace FeatBit.Sdk.Server.Events;

public class DefaultEventBufferTests
{
    [Fact]
    public void GetEventSnapshot()
    {
        var buffer = new DefaultEventBuffer(2);

        buffer.AddEvent(new IntEvent(1));
        buffer.AddEvent(new IntEvent(2));

        var snapshot = buffer.EventsSnapshot;

        buffer.Clear();
        Assert.Equal(0, buffer.Count);
        Assert.True(buffer.IsEmpty);

        // after the buffer is cleared, the snapshot should remain unchanged
        Assert.Equal(2, snapshot.Length);

        Assert.Equal(1, ((IntEvent)snapshot[0]).Value);
        Assert.Equal(2, ((IntEvent)snapshot[1]).Value);
    }

    [Fact]
    public void IgnoreNewEventAfterBufferIsFull()
    {
        var buffer = new DefaultEventBuffer(2);

        Assert.True(buffer.AddEvent(new IntEvent(1)));
        Assert.True(buffer.AddEvent(new IntEvent(2)));

        // buffer is full, the following events should be ignored
        Assert.False(buffer.AddEvent(new IntEvent(3)));
        Assert.False(buffer.AddEvent(new IntEvent(4)));

        Assert.Equal(2, buffer.Count);
    }
}