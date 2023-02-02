using System.Text.Json.Serialization;
using FeatBit.Sdk.Server.Json;

namespace FeatBit.Sdk.Server.Store
{
    public abstract class StorableObject
    {
        [JsonPropertyName("updatedAt")]
        [JsonConverter(typeof(VersionJsonConverter))]
        public long Version { get; protected set; }

        public abstract string StoreKey { get; }
    }
}