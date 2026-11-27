using System;
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
    }
}
