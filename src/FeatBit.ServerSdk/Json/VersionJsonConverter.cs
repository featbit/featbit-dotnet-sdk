using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FeatBit.Sdk.Server.Json
{
    /// <summary>
    /// Convert updatedAt field to version field.
    /// </summary>
    internal class VersionJsonConverter : JsonConverter<long>
    {
        public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return reader.GetDateTimeOffset().ToUnixTimeMilliseconds();
        }

        public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
        {
            var dateTime = DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime;

            writer.WriteString("updatedAt", dateTime);
        }
    }
}