using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal sealed class DefaultEventDispatcher : IEventDispatcher
    {
        private readonly int _maxFlushWorkers;
        private readonly int _maxEventPerRequest;
        private readonly CountdownEvent _flushWorkersCounter;
        private readonly object _flushWorkerCounterLock = new object();

        private readonly IEventBuffer _buffer;
        private readonly IEventSender _sender;
        private readonly IEventSerializer _serializer;

        private readonly AtomicBoolean _stopped;
        internal bool HasStopped => _stopped.Value; // internal for testing

        private readonly ILogger<DefaultEventDispatcher> _logger;

        internal DefaultEventDispatcher(
            FbOptions options,
            BlockingCollection<IEvent> queue,
            IEventBuffer buffer = null,
            IEventSender sender = null,
            IEventSerializer serializer = null)
        {
            _maxFlushWorkers = options.MaxFlushWorker;
            _maxEventPerRequest = options.MaxEventPerRequest;
            _flushWorkersCounter = new CountdownEvent(1);

            // Here we use TaskFactory.StartNew instead of Task.Run() because that allows us to specify the
            // LongRunning option. This option tells the task scheduler that the task is likely to hang on
            // to a thread for a long time, so it should consider growing the thread pool.
            Task.Factory.StartNew(
                () => DispatchLoop(queue),
                TaskCreationOptions.LongRunning
            );

            _logger = options.LoggerFactory.CreateLogger<DefaultEventDispatcher>();

            _buffer = buffer ?? new DefaultEventBuffer(options.MaxEventsInQueue);
            _sender = sender ?? new DefaultEventSender(options);
            _serializer = serializer ?? new DefaultEventSerializer();
            _stopped = new AtomicBoolean();
        }

        private void DispatchLoop(BlockingCollection<IEvent> queue)
        {
            var running = true;
            while (running)
            {
                try
                {
                    var @event = queue.Take();
                    switch (@event)
                    {
                        case PayloadEvent pe:
                            AddEventToBuffer(pe);
                            break;
                        case FlushEvent fe:
                            TriggerFlush(fe);
                            break;
                        case ShutdownEvent se:
                            WaitForFlushes();
                            running = false;
                            se.Complete();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error in event dispatcher thread");
                }
            }
        }

        private void AddEventToBuffer(IEvent @event)
        {
            if (_stopped.Value)
            {
                return;
            }

            _buffer.AddEvent(@event);
        }

        private void TriggerFlush(AsyncEvent @event)
        {
            if (_stopped.Value)
            {
                @event.Complete();
                return;
            }

            if (_buffer.IsEmpty)
            {
                // There are no events to flush. If we don't complete the message, then the async task may never
                // complete (if it had a non-zero positive timeout, then it would complete after the timeout).
                @event.Complete();
                return;
            }

            lock (_flushWorkerCounterLock)
            {
                // Note that this counter will be 1, not 0, when there are no active flush workers.
                // This is because a .NET CountdownEvent can't be reused without explicitly resetting
                // it once it has gone to zero.
                if (_flushWorkersCounter.CurrentCount >= _maxFlushWorkers + 1)
                {
                    // We already have too many workers, so just leave the events as is
                    @event.Complete();
                    return;
                }

                // We haven't hit the limit, we'll go ahead and start a flush task
                _flushWorkersCounter.AddCount(1);
            }

            // get events snapshot then clear original buffer
            var snapshot = _buffer.EventsSnapshot;
            _buffer.Clear();

            Task.Run(async () =>
            {
                try
                {
                    await FlushEventsAsync(snapshot);
                }
                finally
                {
                    _flushWorkersCounter.Signal();
                    @event.Complete();
                }
            });
        }

        private async Task FlushEventsAsync(IEvent[] events)
        {
            var memory = new ReadOnlyMemory<IEvent>(events);

            // split and send
            var total = events.Length;
            for (var i = 0; i < total; i += _maxEventPerRequest)
            {
                var length = Math.Min(_maxEventPerRequest, total - i);
                var slice = memory.Slice(i, length);
                var payload = _serializer.Serialize(slice);

                var deliveryStatus = await _sender.SendAsync(payload);
                if (deliveryStatus == DeliveryStatus.FailedAndMustShutDown)
                {
                    _stopped.CompareAndSet(false, true);
                }
            }
        }

        private void WaitForFlushes()
        {
            if (_stopped.GetAndSet(true))
            {
                return;
            }

            // Our CountdownEvent was initialized with a count of 1, so that's the lowest it can be at this point.
            // Drop the count to zero if there are no active flush tasks.
            _flushWorkersCounter.Signal();
            // Wait until it is zero.
            _flushWorkersCounter.Wait();
            _flushWorkersCounter.Reset(1);
        }

        public void Dispose()
        {
            _flushWorkersCounter?.Dispose();
        }
    }
}