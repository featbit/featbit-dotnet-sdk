using System.Collections.Generic;
using System.Text.Json;
using FeatBit.Sdk.Server.Json;
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

        internal static DataSet FromJsonElement(JsonElement jsonElement)
        {
#if NETCOREAPP3_1
            var rawText = jsonElement.GetRawText();
            var dataSet = JsonSerializer.Deserialize<DataSet>(rawText, ReusableJsonSerializerOptions.Web);
#else
            // JsonElement.Deserialize<TValue> only available on .NET 6.0+
            var dataSet = jsonElement.Deserialize<DataSet>(ReusableJsonSerializerOptions.Web);
#endif
            return dataSet;
        }
    }
}