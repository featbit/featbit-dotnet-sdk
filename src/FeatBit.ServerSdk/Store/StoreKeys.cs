namespace FeatBit.Sdk.Server.Store
{
    internal static class StoreKeys
    {
        public static string ForSegment(string segmentId)
        {
            return $"segment_{segmentId}";
        }

        public static string ForFeatureFlag(string flagKey)
        {
            return $"ff_{flagKey}";
        }
    }
}