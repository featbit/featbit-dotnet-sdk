namespace FeatBit.Sdk.Server
{
    internal delegate bool ValueConverter<TValue>(string value, out TValue converted);

    internal static class ValueConverters
    {
        internal static readonly ValueConverter<bool> Bool = (string value, out bool converted) =>
            bool.TryParse(value, out converted);

        internal static readonly ValueConverter<string> String = (string value, out string converted) =>
        {
            converted = value;
            return true;
        };

        public static readonly ValueConverter<int> Int = (string value, out int converted) =>
            int.TryParse(value, out converted);

        public static readonly ValueConverter<float> Float = (string value, out float converted) =>
            float.TryParse(value, out converted);
    }
}