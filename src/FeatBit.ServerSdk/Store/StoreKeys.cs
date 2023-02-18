namespace FeatBit.Sdk.Server.Store
{
    internal static class StoreKeys
    {
        public const string SegmentPrefix = "segment_";
        public const string FlagPrefix = "ff_";

        public static string ForSegment(string segmentId)
        {
            return $"{SegmentPrefix}{segmentId}";
        }

        public static string ForFeatureFlag(string flagKey)
        {
            return $"{FlagPrefix}{flagKey}";
        }
    }
}