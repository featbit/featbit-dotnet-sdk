namespace FeatBit.Sdk.Server.Model
{
    internal sealed class Variation
    {
        public string Id { get; set; }

        public string Value { get; set; }

        public static readonly Variation Empty = new()
        {
            Id = string.Empty,
            Value = string.Empty
        };
    }
}