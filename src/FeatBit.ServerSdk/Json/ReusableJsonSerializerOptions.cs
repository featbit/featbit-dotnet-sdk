using System.Text.Json;

namespace FeatBit.Sdk.Server.Json
{
    public class ReusableJsonSerializerOptions
    {
#if NETCOREAPP3_1
        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-core-3-1
        // A JsonSerializerOptions constructor that specifies a set of defaults is not available in .NET Core 3.1.
        public static readonly JsonSerializerOptions Web = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
#else
        // https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/configure-options?pivots=dotnet-6-0#web-defaults-for-jsonserializeroptions
        public static readonly JsonSerializerOptions Web = new JsonSerializerOptions(JsonSerializerDefaults.Web);
#endif
    }
}