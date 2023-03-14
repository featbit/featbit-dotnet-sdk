using System;
using System.IO;
using System.Text.Json;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Events
{
    internal class DefaultEventSerializer : IEventSerializer
    {
        public byte[] Serialize(IEvent @event)
        {
            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            WriteEvent(@event, writer);

            writer.Flush();
            return stream.ToArray();
        }

        public byte[] Serialize(ReadOnlyMemory<IEvent> events)
        {
            var span = events.Span;

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream);

            writer.WriteStartArray();
            for (var i = 0; i < span.Length; i++)
            {
                WriteEvent(span[i], writer);
            }

            writer.WriteEndArray();

            writer.Flush();
            return stream.ToArray();
        }

        private static void WriteEvent(IEvent ue, Utf8JsonWriter writer)
        {
            switch (ue)
            {
                case EvalEvent ee:
                    WriteEvalEvent(ee, writer);
                    break;
            }
        }

        private static void WriteEvalEvent(EvalEvent ee, Utf8JsonWriter writer)
        {
            writer.WriteStartObject();

            WriteUser(ee.User, writer);

            writer.WriteStartArray("variations");

            writer.WriteStartObject();
            writer.WriteString("featureFlagKey", ee.FlagKey);
            WriteVariation(ee.Variation, writer);
            writer.WriteNumber("timestamp", ee.Timestamp);
            writer.WriteBoolean("sendToExperiment", ee.SendToExperiment);
            writer.WriteEndObject();

            writer.WriteEndArray();

            writer.WriteEndObject();
        }

        private static void WriteVariation(Variation variation, Utf8JsonWriter writer)
        {
            writer.WriteStartObject("variation");

            writer.WriteString("id", variation.Id);
            writer.WriteString("value", variation.Value);

            writer.WriteEndObject();
        }

        private static void WriteUser(FbUser user, Utf8JsonWriter writer)
        {
            writer.WriteStartObject("user");

            writer.WriteString("keyId", user.Key);
            writer.WriteString("name", user.Name);

            writer.WriteStartArray("customizedProperties");
            foreach (var kv in user.Custom)
            {
                writer.WriteStartObject();
                writer.WriteString("name", kv.Key);
                writer.WriteString("value", kv.Value);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteEndObject();
        }
    }
}