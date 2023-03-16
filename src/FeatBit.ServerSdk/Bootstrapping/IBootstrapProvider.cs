using FeatBit.Sdk.Server.DataSynchronizer;

namespace FeatBit.Sdk.Server.Bootstrapping;

internal interface IBootstrapProvider
{
    DataSet DataSet();
}