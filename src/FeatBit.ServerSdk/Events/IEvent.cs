using System;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Events
{
    internal interface IEvent
    {
    }

    internal abstract class AsyncEvent : IEvent
    {
        private readonly TaskCompletionSource<bool> _innerTcs;
        private readonly Task<bool> _innerTask;

        public bool IsCompleted => _innerTask.IsCompleted;

        internal AsyncEvent()
        {
            _innerTcs = new TaskCompletionSource<bool>();
            _innerTask = _innerTcs.Task;
        }

        internal bool WaitForCompletion(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                _innerTask.Wait();
                return true;
            }

            return _innerTask.Wait(timeout);
        }

        internal Task<bool> WaitForCompletionAsync(TimeSpan timeout)
        {
            if (timeout <= TimeSpan.Zero)
            {
                return _innerTask;
            }

            var timeoutTask = Task.Delay(timeout).ContinueWith(_ => false);
            return Task.WhenAny(_innerTask, timeoutTask).Result;
        }

        internal void Complete()
        {
            _innerTcs.SetResult(true);
        }
    }

    internal sealed class FlushEvent : AsyncEvent
    {
    }

    internal sealed class ShutdownEvent : AsyncEvent
    {
    }

    // posted by a flush worker so the dispatch loop puts events it could not send back into the buffer
    internal sealed class RetainEventsEvent : IEvent
    {
        public IEvent[] Events { get; }

        public RetainEventsEvent(IEvent[] events)
        {
            Events = events;
        }
    }

    internal class PayloadEvent : IEvent
    {
    }

    internal sealed class EvalEvent : PayloadEvent
    {
        public FbUser User { get; set; }

        public string FlagKey { get; set; }

        public long Timestamp { get; set; }

        public Variation Variation { get; set; }

        public EvalEvent(FbUser user, string flagKey, Variation variation)
        {
            User = user;
            FlagKey = flagKey;
            Variation = variation;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    internal sealed class MetricEvent : PayloadEvent
    {
        public const string AppType = "dotnet-server-side";
        public const string Route = "index/metric";
        public const string Type = "CustomEvent";

        public FbUser User { get; set; }

        public string EventName { get; set; }

        public double NumericValue { get; set; }

        public long Timestamp { get; set; }

        public MetricEvent(FbUser user, string eventName, double numericValue)
        {
            User = user;
            EventName = eventName;
            NumericValue = numericValue;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}