using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.DataSynchronizer;

internal sealed class DataSyncProcessor
{
    private readonly IMemoryStore _store;
    private readonly ILogger _logger;

    public DataSyncProcessor(IMemoryStore store, ILogger logger)
    {
        _store = store;
        _logger = logger;
    }

    public DataSyncResult Process(DataSet dataSet)
    {
        switch (dataSet.EventType)
        {
            case DataSet.Full:
            {
                var objects = dataSet.GetStorableObjects();
                _store.Populate(objects);
                var change = new DataChangeEventArgs(DataChangeKind.Full, true, true);

                return new DataSyncResult(applied: true, change);
            }

            case DataSet.Patch:
            {
                var flagsChanged = false;
                var segmentsChanged = false;
                foreach (var item in dataSet.GetStorableObjects())
                {
                    if (_store.Upsert(item))
                    {
                        flagsChanged |= item is FeatureFlag;
                        segmentsChanged |= item is Segment;
                    }
                }

                var change = flagsChanged || segmentsChanged
                    ? new DataChangeEventArgs(DataChangeKind.Patch, flagsChanged, segmentsChanged)
                    : null;

                return new DataSyncResult(applied: true, change);
            }

            default:
            {
                _logger.LogWarning("Unknown data-sync event type: {EventType}", dataSet.EventType);

                return new DataSyncResult(applied: false);
            }
        }
    }
}

internal sealed class DataSyncResult
{
    public bool Applied { get; }

    public DataChangeEventArgs Change { get; }

    public DataSyncResult(bool applied, DataChangeEventArgs change = null)
    {
        Applied = applied;
        Change = change;
    }
}
