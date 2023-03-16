using System;
using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Bootstrapping;

internal sealed class NullBootstrapProvider : IBootstrapProvider
{
    private readonly DataSet _emptySet;

    public NullBootstrapProvider()
    {
        _emptySet = new DataSet
        {
            EventType = DataSynchronizer.DataSet.Full,
            FeatureFlags = Array.Empty<FeatureFlag>(),
            Segments = Array.Empty<Segment>()
        };
    }

    public DataSet DataSet() => _emptySet;
}