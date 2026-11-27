using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.Options;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal sealed partial class DefaultEventDispatcher : IEventDispatcher
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

        // set once every flush worker has finished during shutdown; until then the workers still use
        // _flushWorkersCounter, so Dispose must not dispose it
        private volatile bool _flushWorkersFinished;

        // When the server rejects events (e.g. 401/403/404), delivery is paused instead of stopped forever.
        // Events keep accumulating in the (bounded) buffer and are sent once the pause ends.
        private readonly TimeSpan _initialRejectionBackoff;
        private readonly TimeSpan _maxRejectionBackoff;
        private readonly object _suspensionLock = new object();
        private DateTime _resumeAt = DateTime.MinValue;
        private TimeSpan _nextRejectionBackoff;
        internal bool IsSuspended => DateTime.UtcNow < ResumeAt; // internal for testing
        internal int DroppedOnLastRejection { get; private set; } // internal for testing

        // the dispatch loop's queue: flush workers hand unsent events back through it, since only the loop
        // may touch _buffer
        private readonly BlockingCollection<IEvent> _queue;

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

            _logger = options.LoggerFactory.CreateLogger<DefaultEventDispatcher>();

            _buffer = buffer ?? new DefaultEventBuffer(options.MaxEventsInQueue);
            _sender = sender ?? new DefaultEventSender(options);
            _serializer = serializer ?? new DefaultEventSerializer();
            _stopped = new AtomicBoolean();

            _initialRejectionBackoff = options.EventRejectionBackoff;
            _maxRejectionBackoff = options.EventRejectionMaxBackoff;
            _nextRejectionBackoff = _initialRejectionBackoff;
            _queue = queue;

            // Start the loop only after every field it uses has been assigned.
            // Here we use TaskFactory.StartNew instead of Task.Run() because that allows us to specify the
            // LongRunning option. This option tells the task scheduler that the task is likely to hang on
            // to a thread for a long time, so it should consider growing the thread pool.
            Task.Factory.StartNew(
                () => DispatchLoop(queue),
                TaskCreationOptions.LongRunning
            );
        }

        private DateTime ResumeAt
        {
            get
            {
                lock (_suspensionLock)
                {
                    return _resumeAt;
                }
            }
        }

        private void DispatchLoop(BlockingCollection<IEvent> queue)
        {
            Log.StartDispatchLoop(_logger);

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
                        case RetainEventsEvent re:
                            RetainEvents(re.Events);
                            break;
                        case ShutdownEvent se:
                            WaitForFlushes();
                            _flushWorkersFinished = true;
                            running = false;
                            se.Complete();
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Log.DispatchError(_logger, ex);
                }
            }

            Log.FinishDispatchLoop(_logger);
        }

        private void AddEventToBuffer(IEvent @event)
        {
            if (_stopped.Value)
            {
                return;
            }

            if (_buffer.AddEvent(@event))
            {
                Log.AddedEventToBuffer(_logger);
            }
            else
            {
                Log.ExceededCapacity(_logger);
            }
        }

        // runs on the dispatch loop: put events a flush worker could not send back into the buffer
        private void RetainEvents(IEvent[] events)
        {
            if (_stopped.Value)
            {
                return;
            }

            var retained = 0;
            while (retained < events.Length && _buffer.AddEvent(events[retained]))
            {
                retained++;
            }

            Log.EventsRetained(_logger, retained);
            if (retained < events.Length)
            {
                Log.RetainedEventsDropped(_logger, events.Length - retained);
            }
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
                Log.FlushEmptyBuffer(_logger);
                // There are no events to flush. If we don't complete the message, then the async task may never
                // complete (if it had a non-zero positive timeout, then it would complete after the timeout).
                @event.Complete();
                return;
            }

            if (IsSuspended)
            {
                // Delivery is paused after a rejection; keep the events buffered until the pause ends.
                Log.DeliverySuspended(_logger, ResumeAt);
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
                    Log.TooManyFlushWorkers(_logger);
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
                    Log.EventsFlushed(_logger, snapshot.Length);
                }
            });
        }

        private async Task FlushEventsAsync(IEvent[] events)
        {
            var memory = new ReadOnlyMemory<IEvent>(events);

            // events this flush stops short of sending because another worker's rejection paused delivery
            var unsent = new List<IEvent>();

            // split and send
            var total = events.Length;
            for (var i = 0; i < total; i += _maxEventPerRequest)
            {
                if (unsent.Count > 0 || IsSuspended)
                {
                    // Another flush worker was rejected while this one was sending. These events were never
                    // rejected, but sending them now would be (and would lengthen the pause): keep them for
                    // when delivery resumes instead.
                    unsent.AddRange(memory.Slice(i).ToArray());
                    break;
                }

                var length = Math.Min(_maxEventPerRequest, total - i);
                var slice = memory.Slice(i, length);

                var result = await SendBatchAsync(slice, unsent);
                if (result.Status == DeliveryStatus.Rejected)
                {
                    // Do not keep sending the rest of this flush into a rejection: drop it and pause delivery.
                    // Dropped = what was not delivered from this slice + every slice after it.
                    var remainingAfterSlice = total - i - length;
                    SuspendDelivery(droppedEvents: result.RejectedEvents + remainingAfterSlice);
                    return;
                }
            }

            if (unsent.Count > 0)
            {
                HandBackToBuffer(unsent.ToArray());
            }
        }

        private void HandBackToBuffer(IEvent[] events)
        {
            Log.FlushStoppedByPause(_logger, events.Length);

            bool queued;
            try
            {
                // never blocks: if the queue is full the events are dropped, like any event recorded then
                queued = _queue.TryAdd(new RetainEventsEvent(events));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ObjectDisposedException)
            {
                // shutting down: nothing will send them anyway
                queued = false;
            }

            if (!queued)
            {
                Log.RetainedEventsDropped(_logger, events.Length);
            }
        }

        /// <param name="batch">events to send in one request (split further if the server says it is too large)</param>
        /// <param name="unsent">
        /// receives the events of a split batch that were not sent because delivery was paused in the meantime
        /// </param>
        /// <returns>
        /// The final status, and when it is <see cref="DeliveryStatus.Rejected"/>, how many events of this batch
        /// were not delivered (after a split, the halves sent before the rejection were delivered).
        /// </returns>
        private async Task<(DeliveryStatus Status, int RejectedEvents)> SendBatchAsync(
            ReadOnlyMemory<IEvent> batch,
            List<IEvent> unsent)
        {
            var payload = _serializer.Serialize(batch);
            var status = await _sender.SendAsync(payload);

            switch (status)
            {
                case DeliveryStatus.Succeeded:
                    ResetRejectionBackoff();
                    return (status, 0);

                case DeliveryStatus.Rejected:
                    return (status, batch.Length);

                case DeliveryStatus.PayloadTooLarge when batch.Length > 1:
                {
                    // split the batch in half and send each half on its own
                    var half = batch.Length / 2;
                    var first = await SendBatchAsync(batch.Slice(0, half), unsent);
                    if (first.Status == DeliveryStatus.Rejected)
                    {
                        // the second half is never sent
                        return (first.Status, first.RejectedEvents + (batch.Length - half));
                    }

                    if (unsent.Count > 0 || IsSuspended)
                    {
                        // another worker was rejected while the first half was in flight: keep the second half
                        unsent.AddRange(batch.Slice(half).ToArray());
                        return (DeliveryStatus.Failed, 0);
                    }

                    return await SendBatchAsync(batch.Slice(half), unsent);
                }

                case DeliveryStatus.PayloadTooLarge:
                    Log.SingleEventTooLarge(_logger);
                    return (DeliveryStatus.Failed, 0);

                default:
                    return (status, 0);
            }
        }

        private void SuspendDelivery(int droppedEvents)
        {
            TimeSpan backoff;
            lock (_suspensionLock)
            {
                backoff = _nextRejectionBackoff;
                _resumeAt = DateTime.UtcNow + backoff;
                _nextRejectionBackoff = TimeSpan.FromTicks(Math.Min(backoff.Ticks * 2, _maxRejectionBackoff.Ticks));
                DroppedOnLastRejection = droppedEvents;
            }

            Log.DeliveryRejected(_logger, droppedEvents, backoff);
        }

        private void ResetRejectionBackoff()
        {
            lock (_suspensionLock)
            {
                _nextRejectionBackoff = _initialRejectionBackoff;
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
            // FlushAndClose disposes us when its timeout elapses, which can be while a flush worker is still
            // sending (e.g. retrying a timed-out request). Disposing the counter then would make that worker's
            // Signal() and the dispatch loop's Wait() throw, so shutdown would never complete. In that case the
            // counter is left to the GC; the workers finish and the dispatch loop exits on its own.
            if (_flushWorkersFinished)
            {
                _flushWorkersCounter?.Dispose();
            }
        }
    }
}
