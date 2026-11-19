using System;
using System.Collections.Generic;
using System.Text.Json;
using FeatBit.Sdk.Server.Json;
using FeatBit.Sdk.Server.Model;
using FeatBit.Sdk.Server.Store;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.DataSynchronizer
{
    internal class DataSet
    {
        public const string Full = "full";
        public const string Patch = "patch";

        public string EventType { get; set; }

        public FeatureFlag[] FeatureFlags { get; set; }

        public Segment[] Segments { get; set; }

        /// <summary>
        /// Number of entities in the message that could not be deserialized and were skipped.
        /// </summary>
        public int SkippedCount { get; set; }

        internal IEnumerable<StorableObject> GetStorableObjects()
        {
            var objects = new List<StorableObject>();
            objects.AddRange(FeatureFlags ?? Array.Empty<FeatureFlag>());
            objects.AddRange(Segments ?? Array.Empty<Segment>());

            return objects;
        }

        /// <summary>
        /// Reads a data-sync payload. Each flag and segment is deserialized on its own, so a single malformed
        /// entity is skipped (and logged) instead of causing every other entity in the message to be dropped.
        /// </summary>
        /// <exception cref="JsonException">
        /// The payload itself is malformed: <c>featureFlags</c> or <c>segments</c> is missing or is not an array.
        /// The whole message must be rejected in that case; treating it as empty would let a full sync silently
        /// erase every flag or segment.
        /// </exception>
        internal static DataSet FromJsonElement(JsonElement jsonElement, ILogger logger = null)
        {
            var dataSet = new DataSet
            {
                EventType = TryGetProperty(jsonElement, "eventType", out var eventType) &&
                            eventType.ValueKind == JsonValueKind.String
                    ? eventType.GetString()
                    : null
            };

            var skipped = 0;
            dataSet.FeatureFlags = ReadEntities<FeatureFlag>(jsonElement, "featureFlags", "feature flag", logger, ref skipped);
            dataSet.Segments = ReadEntities<Segment>(jsonElement, "segments", "segment", logger, ref skipped);
            dataSet.SkippedCount = skipped;

            return dataSet;
        }

        private static T[] ReadEntities<T>(
            JsonElement parent,
            string propertyName,
            string entityName,
            ILogger logger,
            ref int skipped) where T : StorableObject
        {
            if (!TryGetProperty(parent, propertyName, out var array))
            {
                throw new JsonException($"Malformed data-sync payload: '{propertyName}' is missing.");
            }

            if (array.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException(
                    $"Malformed data-sync payload: '{propertyName}' must be an array but was {array.ValueKind}."
                );
            }

            var entities = new List<T>(array.GetArrayLength());
            var index = 0;
            foreach (var element in array.EnumerateArray())
            {
                try
                {
                    var entity = element.Deserialize<T>(ReusableJsonSerializerOptions.Web);
                    if (entity != null)
                    {
                        entities.Add(entity);
                    }
                    else
                    {
                        skipped++;
                        logger?.LogError(
                            "Skipped null {EntityType} at {Path}.",
                            entityName, $"$.{propertyName}[{index}]"
                        );
                    }
                }
                catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
                {
                    skipped++;
                    logger?.LogError(
                        ex,
                        "Skipped malformed {EntityType} (key: {Key}, id: {Id}) at {Path}; other entities were applied.",
                        entityName,
                        TryGetString(element, "key"),
                        TryGetString(element, "id"),
                        $"$.{propertyName}[{index}]"
                    );
                }

                index++;
            }

            return entities.ToArray();
        }

        // property lookup is case-insensitive, matching the Web serializer defaults used previously
        private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                value = default;
                return false;
            }

            if (element.TryGetProperty(name, out value))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }

        private static string TryGetString(JsonElement element, string name) =>
            TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
