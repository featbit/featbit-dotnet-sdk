using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.Bootstrapping;

internal interface IBootstrapProvider
{
    DataSet DataSet();

    void Populate(IMemoryStore store);
}