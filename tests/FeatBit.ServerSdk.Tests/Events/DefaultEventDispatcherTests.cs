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
    public void ActiveFlushKeepsItsRemainingEventsWhenAnotherFlushIsRejected()
    {
        // request 1 (worker A's first batch) is held open; request 2 (worker B) is rejected; later ones succeed
        var release = new TaskCompletionSource<DeliveryStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = new ConcurrentFlushTest(maxEventPerRequest: 1, call => call switch
        {
            1 => release.Task,
            2 => Task.FromResult(DeliveryStatus.Rejected),
            _ => Task.FromResult(DeliveryStatus.Succeeded)
        });
        using var dispatcher = test.Dispatcher;

        // worker A: 3 one-event batches, stuck sending the first
        var flushA = test.StartFlush(eventCount: 3, untilCalls: 1);

        // worker B is rejected while A is still sending: delivery pauses
        test.RejectAnotherFlush();

        // A's first batch completes; it must not send its remaining two batches into the pause...
        release.SetResult(DeliveryStatus.Succeeded);
        EnsureAsyncEventComplete(flushA);
        Assert.Equal(2, test.Calls);

        // ...but keeps them, and sends them once delivery resumes
        test.FlushAfterPause();
        Assert.Equal(4, test.Calls);
    }

    [Fact]
    public void SplitBatchKeepsItsSecondHalfWhenAnotherFlushIsRejected()
    {
        // request 1: worker A's 2-event batch is too large; request 2 (first half) is held open;
        // request 3 (worker B) is rejected; later ones succeed
        var release = new TaskCompletionSource<DeliveryStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
        var test = new ConcurrentFlushTest(maxEventPerRequest: 2, call => call switch
        {
            1 => Task.FromResult(DeliveryStatus.PayloadTooLarge),
            2 => release.Task,
            3 => Task.FromResult(DeliveryStatus.Rejected),
            _ => Task.FromResult(DeliveryStatus.Succeeded)
        });
        using var dispatcher = test.Dispatcher;

        var flushA = test.StartFlush(eventCount: 2, untilCalls: 2);
        test.RejectAnotherFlush();

        // the first half completes; the second half must not be sent into the pause...
        release.SetResult(DeliveryStatus.Succeeded);
        EnsureAsyncEventComplete(flushA);
        Assert.Equal(3, test.Calls);

        // ...but is kept, and sent once delivery resumes
        test.FlushAfterPause();
        Assert.Equal(4, test.Calls);
    }

    // Two flush workers: worker A is held mid-send while worker B is rejected.
    private sealed class ConcurrentFlushTest
    {
        private readonly BlockingCollection<IEvent> _queue = new();
        private int _calls;

        public DefaultEventDispatcher Dispatcher { get; }
        public int Calls => Volatile.Read(ref _calls);

        public ConcurrentFlushTest(int maxEventPerRequest, Func<int, Task<DeliveryStatus>> respond)
        {
            var options = new FbOptionsBuilder("fake-secret")
                .MaxFlushWorker(2)
                .MaxEventPerRequest(maxEventPerRequest)
                .Build();
            // long enough that worker A always finishes inside the pause, short enough to wait out
            options.EventRejectionBackoff = TimeSpan.FromSeconds(2);

            var sender = new Mock<IEventSender>();
            sender.Setup(x => x.SendAsync(It.IsAny<byte[]>()))
                .Returns(() => respond(Interlocked.Increment(ref _calls)));

            Dispatcher = new DefaultEventDispatcher(options, _queue, sender: sender.Object);
        }

        public FlushEvent StartFlush(int eventCount, int untilCalls)
        {
            for (var i = 0; i < eventCount; i++) _queue.Add(new IntEvent(i));
            var flush = new FlushEvent();
            _queue.Add(flush);
            Assert.True(SpinWait.SpinUntil(() => Calls == untilCalls, 1000));
            return flush;
        }

        public void RejectAnotherFlush()
        {
            _queue.Add(new IntEvent(100));
            FlushAndWaitComplete(_queue);
            Assert.True(Dispatcher.IsSuspended);
        }

        public void FlushAfterPause()
        {
            Assert.True(SpinWait.SpinUntil(() => !Dispatcher.IsSuspended, 5000));
            FlushAndWaitComplete(_queue);
        }
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