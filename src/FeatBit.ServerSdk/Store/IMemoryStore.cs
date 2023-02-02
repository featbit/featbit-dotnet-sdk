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
        /// <param name="objects">a list of <see cref="StorableObject"/> instances with
        /// their store keys.</param>
        void Populate(IEnumerable<StorableObject> objects);

        /// <summary>
        /// Retrieves an object from the store, if available.
        /// </summary>
        /// <param name="key">the unique key of the object within the store</param>
        /// <returns>The object; null if the key is unknown</returns>
        TObject Get<TObject>(string key) where TObject : class;

        /// <summary>
        /// Updates or inserts an item in the store. For updates, the object will only be
        /// updated if the existing version is less than the new version.
        /// </summary>
        /// <remarks>
        /// The SDK may pass an <see cref="StorableObject"/> that contains a null, to
        /// represent a placeholder for a deleted item. In that case, assuming the version
        /// is greater than any existing version of that item, the store should retain that
        /// placeholder rather than simply not storing anything.
        /// </remarks>
        /// <param name="storableObj">the item to insert or update</param>
        /// <returns>true if the item was updated; false if it was not updated because the
        /// store contains an equal or greater version</returns>
        bool Upsert(StorableObject storableObj);
    }
}