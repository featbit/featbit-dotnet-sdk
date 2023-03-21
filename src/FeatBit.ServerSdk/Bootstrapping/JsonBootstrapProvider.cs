using System.Text.Json;
using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Bootstrapping;

internal sealed class JsonBootstrapProvider : IBootstrapProvider
{
    private readonly DataSet _dataSet;

    public JsonBootstrapProvider(string json)
    {
        using var jsonDocument = JsonDocument.Parse(json);
        var root = jsonDocument.RootElement;
        _dataSet = DataSynchronizer.DataSet.FromJsonElement(root.GetProperty("data"));
    }

    public DataSet DataSet() => _dataSet;

    public void Populate(IMemoryStore store)
    {
        var objects = _dataSet.GetStorableObjects();
        store.Populate(objects);
    }
}