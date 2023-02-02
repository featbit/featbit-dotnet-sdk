using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FeatBit.Sdk.Server.Store
{
    public class DefaultMemoryStore : IMemoryStore
    {
        public bool Populated { get; private set; }

        private readonly object _populateLock = new object();

        private volatile ConcurrentDictionary<string, ObjectDescriptor> _items =
            new ConcurrentDictionary<string, ObjectDescriptor>();

        public void Populate(IEnumerable<IStorableObject> objects)
        {
            lock (_populateLock)
            {
                var kvs = objects.Select(
                    x => new KeyValuePair<string, ObjectDescriptor>(x.StoreKey, x.Descriptor())
                );

                _items = new ConcurrentDictionary<string, ObjectDescriptor>(kvs);
                Populated = true;
            }
        }

        public TObject Get<TObject>(string key) where TObject : class
        {
            if (_items.TryGetValue(key, out var descriptor) && descriptor.Item is TObject tObject)
            {
                return tObject;
            }

            return null;
        }
    }
}