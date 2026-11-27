using System;
using System.Threading;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Utils;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal sealed class DataSyncTimeoutWatchdog
    {
        private readonly TimeSpan _timeout;
        private CancellationTokenSource _pending;

        public DataSyncTimeoutWatchdog(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public void Start(Action<TimeSpan> onTimeout)
        {
            var pending = new CancellationTokenSource();
            var delay = Task.Delay(_timeout, pending.Token);

            var previous = Interlocked.Exchange(ref _pending, pending);
            CancelAndDispose(previous);

            WatchAsync(pending, delay, onTimeout).Forget();
        }

        public void Cancel()
        {
            var previous = Interlocked.Exchange(ref _pending, null);
            CancelAndDispose(previous);
        }

        private async Task WatchAsync(CancellationTokenSource pending, Task delay, Action<TimeSpan> onTimeout)
        {
            try
            {
                await delay.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // Only the operation that removes this timer owns its disposal and can act on it.
            if (Interlocked.CompareExchange(ref _pending, null, pending) == pending)
            {
                pending.Dispose();
                onTimeout(_timeout);
            }
        }

        private static void CancelAndDispose(CancellationTokenSource pending)
        {
            if (pending == null)
            {
                return;
            }

            pending.Cancel();
            pending.Dispose();
        }
    }
}
