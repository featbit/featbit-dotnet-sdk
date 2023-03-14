using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal sealed class DefaultEventProcessor : IEventProcessor
    {
        private readonly BlockingCollection<IEvent> _eventQueue;
        private readonly Timer _flushTimer;
        private readonly IEventDispatcher _eventDispatcher;
        private readonly ILogger<DefaultEventProcessor> _logger;

        private readonly AtomicBoolean _closed = new AtomicBoolean();
        internal bool HasClosed => _closed.Value; // internal for testing
        private readonly AtomicBoolean _capacityExceeded = new AtomicBoolean();

        public DefaultEventProcessor(
            FbOptions options,
            ILogger<DefaultEventProcessor> logger = null,
            Func<FbOptions, BlockingCollection<IEvent>, IEventDispatcher> dispatcherFactory = null)
        {
            _eventQueue = new BlockingCollection<IEvent>(options.MaxEventsInQueue);
            _flushTimer = new Timer(AutoFlush, null, options.AutoFlushInterval, options.AutoFlushInterval);

            var factory = dispatcherFactory ?? DefaultEventDispatcherFactory;
            _eventDispatcher = factory(options, _eventQueue);

            _logger = logger ?? options.LoggerFactory.CreateLogger<DefaultEventProcessor>();
        }

        private static IEventDispatcher DefaultEventDispatcherFactory(FbOptions options, BlockingCollection<IEvent> queue)
        {
            return new DefaultEventDispatcher(options, queue);
        }

        public bool Record(IEvent @event)
        {
            if (@event == null)
            {
                return false;
            }

            try
            {
                if (_eventQueue.TryAdd(@event))
                {
                    _capacityExceeded.GetAndSet(false);
                }
                else
                {
                    if (!_capacityExceeded.GetAndSet(true))
                    {
                        // The main thread is seriously backed up with not-yet-processed events. 
                        _logger.LogWarning(
                            "Events are being produced faster than they can be processed. We shouldn't see this."
                        );
                    }

                    // If the message is a flush message, then it could never be completed if we cannot
                    // add it to the queue. So we are going to complete it here to prevent the calling
                    // code from hanging indefinitely.
                    if (@event is FlushEvent flushEvent)
                    {
                        flushEvent.Complete();
                    }

                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding event in a queue.");
                return false;
            }

            return true;
        }

        public void Flush()
        {
            Record(new FlushEvent());
        }

        public bool FlushAndWait(TimeSpan timeout)
        {
            var flush = new FlushEvent();
            Record(flush);
            return flush.WaitForCompletion(timeout);
        }

        public async Task<bool> FlushAndWaitAsync(TimeSpan timeout)
        {
            var flush = new FlushEvent();
            Record(flush);
            return await flush.WaitForCompletionAsync(timeout);
        }

        public void FlushAndClose(TimeSpan timeout)
        {
            if (_closed.GetAndSet(true))
            {
                return;
            }

            _flushTimer?.Dispose();
            _eventDispatcher?.Dispose();

            Record(new FlushEvent());

            var shutdown = new ShutdownEvent();
            Record(shutdown);

            shutdown.WaitForCompletion(timeout);

            _eventQueue.CompleteAdding();
            _eventQueue.Dispose();
        }

        private void AutoFlush(object stateInfo)
        {
            Flush();
        }
    }
}