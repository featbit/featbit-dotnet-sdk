using System;
using System.Threading.Tasks;
using FeatBit.Sdk.Server.Concurrent;

namespace FeatBit.Sdk.Server.DataSynchronizer;

internal sealed class NullDataSynchronizer : IDataSynchronizer
{
    private readonly StatusManager<DataSynchronizerStatus> _statusManager;

    public bool Initialized => true;
    public DataSynchronizerStatus Status => _statusManager.Status;
    public event Action<DataSynchronizerStatus> StatusChanged;

    public NullDataSynchronizer()
    {
        _statusManager = new StatusManager<DataSynchronizerStatus>(
            DataSynchronizerStatus.Stable,
            OnStatusChanged
        );
    }

    public Task<bool> StartAsync()
    {
        _statusManager.SetStatus(DataSynchronizerStatus.Stable);
        return Task.FromResult(true);
    }

    public Task StopAsync()
    {
        _statusManager.SetStatus(DataSynchronizerStatus.Stopped);
        return Task.CompletedTask;
    }

    private void OnStatusChanged(DataSynchronizerStatus status) => StatusChanged?.Invoke(status);
}