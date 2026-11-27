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

        // wait for both events: checking after the first would race the dispatch loop adding the second
        Assert.True(SpinWait.SpinUntil(() => mockBuffer.Invocations.Count >= 2, 1000));
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
    public void RejectionPausesDeliveryThenResumes()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();
        options.EventRejectionBackoff = TimeSpan.FromMilliseconds(300);

        var mockSender = new Mock<IEventSender>();
        mockSender.SetupSequence(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(Task.FromResult(DeliveryStatus.Rejected))
            .Returns(Task.FromResult(DeliveryStatus.Succeeded));

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        // first flush is rejected: delivery pauses, but the dispatcher is not stopped
        AddEventThenFlush();
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
        Assert.True(dispatcher.IsSuspended);
        Assert.False(dispatcher.HasStopped);

        // while paused, events are buffered rather than sent
        mockSender.Invocations.Clear();
        AddEventThenFlush();
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Never);

        // once the pause ends, buffered events are delivered
        Assert.True(SpinWait.SpinUntil(() => !dispatcher.IsSuspended, 2000));
        FlushAndWaitComplete(queue);
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);

        void AddEventThenFlush()
        {
            queue.Add(new IntEvent(1));
            FlushAndWaitComplete(queue);
        }
    }

    [Fact]
    public void RejectionStopsSendingTheRestOfTheFlush()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").MaxEventPerRequest(1).Build();

        var mockSender = new Mock<IEventSender>();
        mockSender.Setup(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(Task.FromResult(DeliveryStatus.Rejected));

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        for (var i = 0; i < 5; i++) queue.Add(new IntEvent(i));
        FlushAndWaitComplete(queue);

        // one request rejected; the other four batches are not sent into the rejection
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public void ActiveFlushStopsWhenAnotherFlushIsRejected()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").MaxFlushWorker(2).MaxEventPerRequest(1).Build();

        // the first request (worker A's first batch) is held until released; every later request is rejected
        var release = new TaskCompletionSource<DeliveryStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var mockSender = new Mock<IEventSender>();
        mockSender.Setup(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(() => Interlocked.Increment(ref calls) == 1
                ? release.Task
                : Task.FromResult(DeliveryStatus.Rejected));

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        // worker A: 3 one-event batches, stuck sending the first
        for (var i = 0; i < 3; i++) queue.Add(new IntEvent(i));
        var flushA = new FlushEvent();
        queue.Add(flushA);
        Assert.True(SpinWait.SpinUntil(() => Volatile.Read(ref calls) == 1, 1000));

        // worker B is rejected while A is still sending: delivery pauses
        queue.Add(new IntEvent(3));
        FlushAndWaitComplete(queue);
        Assert.True(dispatcher.IsSuspended);

        // A's first batch completes; it must not send its remaining two batches into the pause
        release.SetResult(DeliveryStatus.Succeeded);
        EnsureAsyncEventComplete(flushA);
        Assert.Equal(2, Volatile.Read(ref calls));
    }

    [Fact]
    public void DroppedCountExcludesHalvesDeliveredBeforeARejection()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").MaxEventPerRequest(4).Build();

        // 8 events = two 4-event slices. Slice 1: 413, first half delivered, second half rejected.
        var mockSender = new Mock<IEventSender>();
        mockSender.SetupSequence(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(Task.FromResult(DeliveryStatus.PayloadTooLarge))
            .Returns(Task.FromResult(DeliveryStatus.Succeeded))
            .Returns(Task.FromResult(DeliveryStatus.Rejected));

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        for (var i = 0; i < 8; i++) queue.Add(new IntEvent(i));
        FlushAndWaitComplete(queue);

        // 2 rejected from slice 1 + 4 never sent from slice 2; the 2 delivered events are not counted
        Assert.Equal(6, dispatcher.DroppedOnLastRejection);
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Exactly(3));
    }

    [Fact]
    public void ShutdownCompletesWhenDisposedWhileAFlushIsStillSending()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        // a send that is still in progress (e.g. retrying a timed-out request) when the close timeout elapses
        var release = new TaskCompletionSource<DeliveryStatus>();
        var mockSender = new Mock<IEventSender>();
        mockSender.Setup(x => x.SendAsync(It.IsAny<byte[]>())).Returns(release.Task);

        var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        queue.Add(new IntEvent(1));
        queue.Add(new FlushEvent());
        Assert.True(SpinWait.SpinUntil(() => mockSender.Invocations.Count == 1, 1000));

        var shutdown = new ShutdownEvent();
        queue.Add(shutdown);

        // FlushAndClose gave up waiting and disposes the dispatcher while the worker is still sending
        dispatcher.Dispose();

        release.SetResult(DeliveryStatus.Succeeded);

        // previously the worker's Signal() and the loop's Wait() hit a disposed CountdownEvent and shutdown hung
        Assert.True(SpinWait.SpinUntil(() => shutdown.IsCompleted, 2000), "shutdown never completed");
    }

    [Fact]
    public void PayloadTooLargeSplitsTheBatch()
    {
        var queue = new BlockingCollection<IEvent>();
        var options = new FbOptionsBuilder("fake-secret").Build();

        var mockSender = new Mock<IEventSender>();
        mockSender.SetupSequence(x => x.SendAsync(It.IsAny<byte[]>()))
            .Returns(Task.FromResult(DeliveryStatus.PayloadTooLarge))
            .Returns(Task.FromResult(DeliveryStatus.Succeeded))
            .Returns(Task.FromResult(DeliveryStatus.Succeeded));

        using var dispatcher = new DefaultEventDispatcher(options, queue, sender: mockSender.Object);

        for (var i = 0; i < 4; i++) queue.Add(new IntEvent(i));
        FlushAndWaitComplete(queue);

        // the 4-event batch is rejected as too large, then sent as two 2-event halves
        mockSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Exactly(3));
        Assert.False(dispatcher.IsSuspended);
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