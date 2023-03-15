using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.DataSynchronizer;

internal sealed class NullDataSynchronizer : IDataSynchronizer
{
    public bool Initialized => true;

    public Task<bool> StartAsync()
    {
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        return Task.CompletedTask;
    }
}