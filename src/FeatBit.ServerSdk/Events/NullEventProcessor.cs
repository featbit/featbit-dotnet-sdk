using System;
using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.Events;

internal sealed class NullEventProcessor : IEventProcessor
{
    public bool Record(IEvent @event) => true;

    public void Flush()
    {
    }

    public bool FlushAndWait(TimeSpan timeout)
    {
        return true;
    }

    public Task<bool> FlushAndWaitAsync(TimeSpan timeout)
    {
        return Task.FromResult(true);
    }

    public void FlushAndClose(TimeSpan timeout)
    {
    }
}