using System;
using System.Collections.Generic;
using System.Linq;

namespace FeatBit.Sdk.Server.Store
{
    public class DefaultMemoryStore : IMemoryStore
    {
        public bool Populated { get; private set; }

        private readonly object _writeLock = new object();

        private volatile Dictionary<string, StorableObject> _items =
            new Dictionary<string, StorableObject>();

        public void Populate(IEnumerable<StorableObject> objects)
        {
            lock (_writeLock)
            {
                _items = objects.ToDictionary(storableObj => storableObj.StoreKey, storableObj => storableObj);
                Populated = true;
            }
        }

        public TObject Get<TObject>(string key) where TObject : class
        {
            if (_items.TryGetValue(key, out var obj) && obj is TObject tObject)
            {
                return tObject;
            }

            return null;
        }

        public ICollection<TObject> Find<TObject>(Func<StorableObject, bool> predicate)
        {
            var result = new List<TObject>();

            foreach (var value in _items.Values.Where(predicate))
            {
                if (value is TObject tObject)
                {
                    result.Add(tObject);
                }
            }

            return result;
        }

        public bool Upsert(StorableObject storableObj)
        {
            var key = storableObj.StoreKey;

            lock (_writeLock)
            {
                // update item
                if (_items.TryGetValue(key, out var existed))
                {
                    if (existed.Version >= storableObj.Version)
                    {
                        return false;
                    }

                    _items[key] = storableObj;
                    return true;
                }

                // add item
                _items.Add(key, storableObj);
                return true;
            }
        }

        public long Version()
        {
            var values = _items.Values;

            return values.Count == 0 ? 0 : values.Max(x => x.Version);
        }
    }
}