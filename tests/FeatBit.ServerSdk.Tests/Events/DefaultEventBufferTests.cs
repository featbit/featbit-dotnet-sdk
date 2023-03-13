using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Events;

public class DefaultEventBufferTests
{
    [Fact]
    public void GetEventSnapshot()
    {
        var buffer = new DefaultEventBuffer(2, NullLogger.Instance);

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
        const int capacity = 2;
        var loggerMock = new Mock<ILogger>();

        var buffer = new DefaultEventBuffer(capacity, loggerMock.Object);

        buffer.AddEvent(new IntEvent(1));
        buffer.AddEvent(new IntEvent(2));

        // buffer is full, the following events should be ignored and trigger an warning
        buffer.AddEvent(new IntEvent(3));
        buffer.AddEvent(new IntEvent(4));

        Assert.Equal(2, buffer.Count);
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Exactly(2)
        );
    }
}