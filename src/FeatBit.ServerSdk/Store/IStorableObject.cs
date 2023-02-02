namespace FeatBit.Sdk.Server.Store
{
    public interface IStorableObject
    {
        string StoreKey { get; }

        ObjectDescriptor Descriptor();
    }
}