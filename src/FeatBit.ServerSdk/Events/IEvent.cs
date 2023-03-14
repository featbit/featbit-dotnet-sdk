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

    internal class PayloadEvent : IEvent
    {
    }

    internal sealed class EvalEvent : PayloadEvent
    {
        public FbUser User { get; set; }

        public string FlagKey { get; set; }

        public long Timestamp { get; set; }

        public Variation Variation { get; set; }

        public bool SendToExperiment { get; set; }

        public EvalEvent(FbUser user, string flagKey, Variation variation, bool sendToExperiment)
        {
            User = user;
            FlagKey = flagKey;
            Variation = variation;
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            SendToExperiment = sendToExperiment;
        }
    }
}