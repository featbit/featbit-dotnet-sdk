using System.Collections.Generic;

namespace FeatBit.Sdk.Server.Store
{
    /// <summary>
    /// Interface for a data storage that holds feature flags, segments or any other related data received by the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Implementations must be thread-safe.
    /// </para>
    /// </remarks>
    public interface IMemoryStore
    {
        /// <summary>
        /// Indicates whether this store has been populated with any data yet.
        /// </summary>
        /// <returns>true if the store contains data</returns>
        bool Populated { get; }

        /// <summary>
        /// Overwrites the store's contents with a set of new items.
        /// </summary>
        /// <remarks>
        /// <para>
        /// All previous data will be discarded, regardless of versioning.
        /// </para>
        /// </remarks>
        /// <param name="objects">a list of <see cref="IStorableObject"/> instances.</param>
        void Populate(IEnumerable<IStorableObject> objects);

        /// <summary>
        /// Retrieves an object from the store, if available.
        /// </summary>
        /// <param name="key">the unique key of the object within the store</param>
        /// <returns>The object; null if the key is unknown</returns>
        TObject Get<TObject>(string key) where TObject : class;
    }
}