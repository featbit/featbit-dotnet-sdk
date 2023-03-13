using System.Collections.Concurrent;
using FeatBit.Sdk.Server.Options;

namespace FeatBit.Sdk.Server.Events;

public class DefaultEventDispatcherTests
{
    [Fact]
    public void NewAndDispose()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        using var dispatcher = new DefaultEventDispatcher(options, queue);
    }

    [Fact]
    public void AddEvent()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        var mockBuffer = new Mock<IEventBuffer>();

        using var dispatcher = new DefaultEventDispatcher(options, queue, buffer: mockBuffer.Object);

        queue.Add(new IntEvent(1));
        queue.Add(new IntEvent(2));

        mockBuffer.Verify(x => x.AddEvent(It.IsAny<IEvent>()), Times.Exactly(2));
    }

    [Fact]
    public void Flush()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        var mockSender = new Mock<IEventSender>();

        using var dispatcher =
            new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        queue.Add(new IntEvent(1));
        queue.Add(new IntEvent(2));

        FlushAndWaitComplete(queue);

        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public void FlushEmptyBuffer()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        using var dispatcher = new DefaultEventDispatcher(options, queue);

        FlushAndWaitComplete(queue);
    }

    [Fact]
    public void Shutdown()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        using var dispatcher = new DefaultEventDispatcher(options, queue);

        var shutdownEvent = new ShutdownEvent();
        queue.Add(shutdownEvent);
        EnsureAsyncEventComplete(shutdownEvent);
    }

    [Fact]
    public void EventSenderStopDispatcher()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        var mockSender = new Mock<IEventSender>();
        mockSender.Setup(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(Task.FromResult(DeliveryStatus.FailedAndMustShutDown));

        // Create a dispatcher with a mock sender that always fails
        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        // Add an event and flush it to trigger the sender
        AddEventThenFlush();
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);

        // Clear previous invocations for later verification
        mockSender.Invocations.Clear();

        // Check if dispatcher stopped after event sender return FailedAndMustShutDown
        Assert.True(dispatcher.HasStopped);

        // Check if add and flush operations are no-op after dispatcher has stopped
        AddEventThenFlush();
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Never);

        void AddEventThenFlush()
        {
            queue.Add(new IntEvent(1));
            FlushAndWaitComplete(queue);
        }
    }

    [Theory]
    [InlineData(5, 3, 1)]
    [InlineData(3, 12, 4)]
    [InlineData(5, 12, 3)]
    public void SendEventsInMultiBatch(int eventPerRequest, int totalEvents, int expectedBatch)
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret")
            .MaxEventPerRequest(eventPerRequest)
            .Build();

        var mockSender = new Mock<IEventSender>();

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        for (var i = 0; i < totalEvents; i++)
        {
            queue.Add(new IntEvent(i));
        }

        FlushAndWaitComplete(queue);
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Exactly(expectedBatch));
    }

    private static void FlushAndWaitComplete(BlockingCollection<IEvent> queue)
    {
        var flushEvent = new FlushEvent();
        queue.Add(flushEvent);
        EnsureAsyncEventComplete(flushEvent);
    }

    private static void EnsureAsyncEventComplete(AsyncEvent asyncEvent)
    {
        Assert.True(SpinWait.SpinUntil(() => asyncEvent.IsCompleted, 1000));
    }
}