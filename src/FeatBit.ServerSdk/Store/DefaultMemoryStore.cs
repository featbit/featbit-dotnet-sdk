using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatBit.Sdk.Server.Store
{
    public class DefaultMemoryStore : IMemoryStore
    {
        private readonly object _writeLock = new();
        private volatile Snapshot _snapshot = new(new Dictionary<string, StorableObject>(), false, 0);

        public bool Populated => _snapshot.Populated;

        public void Populate(IEnumerable<StorableObject> objects)
        {
            lock (_writeLock)
            {
                var items = new Dictionary<string, StorableObject>();
                foreach (var storableObj in objects)
                {
                    var key = storableObj.StoreKey;
                    if (!items.TryGetValue(key, out var existing) || existing.Version <= storableObj.Version)
                    {
                        items[key] = storableObj;
                    }
                }

                var version = items.Count == 0 ? 0 : items.Values.Max(x => x.Version);
                _snapshot = new Snapshot(items, true, version);
            }
        }

        public TObject Get<TObject>(string key) where TObject : class
        {
            var snapshot = _snapshot;
            if (snapshot.Items.TryGetValue(key, out var obj) && !obj.IsArchived && obj is TObject tObject)
            {
                return tObject;
            }

            return null;
        }

        public ICollection<TObject> Find<TObject>(Func<StorableObject, bool> predicate)
        {
            var snapshot = _snapshot;

            var result = new List<TObject>();
            foreach (var value in snapshot.Items.Values.Where(predicate))
            {
                if (!value.IsArchived && value is TObject tObject)
                {
                    result.Add(tObject);
                }
            }

            return result;
        }

        public bool Upsert(StorableObject storableObj)
        {
            lock (_writeLock)
            {
                var snapshot = _snapshot;

                var key = storableObj.StoreKey;
                if (snapshot.Items.TryGetValue(key, out var existing) && existing.Version > storableObj.Version)
                {
                    return false;
                }

                var items = new Dictionary<string, StorableObject>(snapshot.Items)
                {
                    [key] = storableObj
                };

                var version = snapshot.Items.Count == 0
                    ? storableObj.Version
                    : Math.Max(snapshot.Version, storableObj.Version);

                _snapshot = new Snapshot(items, snapshot.Populated, version);
                return true;
            }
        }

        public long Version() => _snapshot.Version;

        // Published dictionaries and their entries are never modified by the store.
        private sealed class Snapshot
        {
            public readonly Dictionary<string, StorableObject> Items;
            public readonly bool Populated;
            public readonly long Version;

            public Snapshot(Dictionary<string, StorableObject> items, bool populated, long version)
            {
                Items = items;
                Populated = populated;
                Version = version;
            }
        }
    }
}
