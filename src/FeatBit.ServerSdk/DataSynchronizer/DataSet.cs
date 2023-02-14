using System.Collections.Generic;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal class DataSet
    {
        public const string Full = "full";
        public const string Patch = "patch";

        public string EventType { get; set; }

        public FeatureFlag[] FeatureFlags { get; set; }

        public Segment[] Segments { get; set; }

        internal IEnumerable<StorableObject> GetStorableObjects()
        {
            var objects = new List<StorableObject>();
            objects.AddRange(FeatureFlags);
            objects.AddRange(Segments);

            return objects;
        }
    }
}