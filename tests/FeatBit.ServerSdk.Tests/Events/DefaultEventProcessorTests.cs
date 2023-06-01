using System.Collections.Concurrent;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events;

public class DefaultEventProcessorTests
{
    [Fact]
    public void StartAndClose()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var processor = new DefaultEventProcessor(options);

        // this should complete immediately
        processor.FlushAndClose(TimeSpan.FromMilliseconds(100));

        Assert.True(processor.HasClosed);
    }

    [Fact]
    public void CloseAnProcessorMultiTimes()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var processor = new DefaultEventProcessor(options);

        processor.FlushAndClose(TimeSpan.FromMilliseconds(100));
        processor.FlushAndClose(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void RecordEvent()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var processor = new DefaultEventProcessor(options);

        Assert.True(processor.Record(new IntEvent(1)));
    }

    [Fact]
    public void RecordNullEvent()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var processor = new DefaultEventProcessor(options);

        Assert.False(processor.Record(null));
    }

    [Fact]
    public void ExceedCapacity()
    {
        var loggerMock = new Mock<ILogger<DefaultEventProcessor>>();

        var options = new FbOptionsBuilder("secret")
            // set max queue size to 2
            .MaxEventsInQueue(2)
            .Build();

        // create a dispatcher that will not consume the processor's message so that processor's queue can be full
        var dispatcher = new DefaultEventDispatcher(options, new BlockingCollection<IEvent>());
        var processor = new DefaultEventProcessor(options, loggerMock.Object, (_, _) => dispatcher);

        Assert.True(processor.Record(new IntEvent(1)));
        Assert.True(processor.Record(new IntEvent(2)));

        // the processor rejects new events when its queue is full
        Assert.False(processor.Record(new IntEvent(3)));

        // the processor will directly complete any flush event that is rejected.
        var flushEvent = new FlushEvent();
        Assert.False(processor.Record(flushEvent));
        Assert.True(flushEvent.IsCompleted);

        // verify warning logged once
        loggerMock.Verify(
            logger => logger.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()
            ),
            Times.Once
        );
    }

    [Fact]
    public void WaitFlush()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var mockedSender = new Mock<IEventSender>();

        var processor = new DefaultEventProcessor(
            options,
            dispatcherFactory: (opts, queue) => new DefaultEventDispatcher(opts, queue, sender: mockedSender.Object)
        );

        processor.Record(new IntEvent(1));
        var flushedInTime = processor.FlushAndWait(TimeSpan.FromMilliseconds(1000));

        Assert.True(flushedInTime);
        mockedSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task WaitFlushAsync()
    {
        var options = new FbOptionsBuilder("secret").Build();
        var mockedSender = new Mock<IEventSender>();

        var processor = new DefaultEventProcessor(
            options,
            dispatcherFactory: (opts, queue) => new DefaultEventDispatcher(opts, queue, sender: mockedSender.Object)
        );

        processor.Record(new IntEvent(1));
        var flushedInTime = await processor.FlushAndWaitAsync(TimeSpan.FromMilliseconds(100));

        Assert.True(flushedInTime);
        mockedSender.Verify(x => x.SendAsync(It.IsAny<byte[]>()), Times.Once);
    }
}