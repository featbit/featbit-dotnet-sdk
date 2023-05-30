using System.Text.Json;
using FeatBit.Sdk.Server.Json;

namespace FeatBit.Sdk.Server.Model;

[UsesVerify]
public class DeserializationTests
{
    [Fact]
    public Task DeserializeFeatureFlag()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Model", "one-flag.json"));
        var flag = JsonSerializer.Deserialize<FeatureFlag>(json, ReusableJsonSerializerOptions.Web);

        return Verify(flag);
    }

    [Fact]
    public Task DeserializeSegment()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Model", "one-segment.json"));
        var segment = JsonSerializer.Deserialize<Segment>(json, ReusableJsonSerializerOptions.Web);

        return Verify(segment);
    }
}