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
    }
}