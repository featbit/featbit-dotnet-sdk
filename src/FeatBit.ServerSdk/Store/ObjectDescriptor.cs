namespace FeatBit.Sdk.Server.Store
{
    /// <summary>
    /// A versioned item (or placeholder) storable in an <see cref="IMemoryStore"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is used for data stores that directly store objects as-is, as the default
    /// in-memory store does. Items are typed as <see cref="object"/>; the store should
    /// not know or care what the actual object is.
    /// </para>
    /// </remarks>
    public class ObjectDescriptor
    {
        /// <summary>
        /// The version of this data
        /// </summary>
        public long Version { get; set; }

        /// <summary>
        /// The data item, or null if this is a placeholder for a deleted item.
        /// </summary>
        public object Item { get; set; }

        /// <summary>
        /// Constructs an instance.
        /// </summary>
        /// <param name="version">the version number</param>
        /// <param name="item">the data item, or null for a deleted item</param>
        public ObjectDescriptor(long version, object item)
        {
            Version = version;
            Item = item;
        }
    }
}