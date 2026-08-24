using System.Text.Json;

namespace FeatBit.Sdk.Server.Json
{
    public static class ReusableJsonSerializerOptions
    {
        public static readonly JsonSerializerOptions Web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
    }
}
