using System;

namespace FeatBit.Sdk.Server.DataSynchronizer;

internal interface IDataChangeNotifier
{
    event EventHandler<FeatureDataChangedEventArgs> DataChanged;
}
