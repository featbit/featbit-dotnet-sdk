using System;

namespace FeatBit.Sdk.Server
{
    internal static class ValueConverters
    {
        internal static readonly Func<string, bool> Bool = value =>
            string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

        internal static readonly Func<string, string> String = value => value;
    }
}