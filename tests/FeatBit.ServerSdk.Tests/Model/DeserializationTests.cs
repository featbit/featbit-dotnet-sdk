using System.Text.Json;
using FeatBit.Sdk.Server.Json;

namespace FeatBit.Sdk.Server.Model;

[UsesVerify]
public class DeserializationTests
{
    [Fact]
    public Task DeserializeFeatureFlag()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Model", "flags.json"));
        var flags = JsonSerializer.Deserialize<FeatureFlag[]>(json, ReusableJsonSerializerOptions.Web);

        Assert.Collection(flags,
            flag => Assert.True(flag.InsightsEnabled),
            flag => Assert.True(flag.InsightsEnabled),
            flag => Assert.False(flag.InsightsEnabled));

        return Verify(flags);
    }

    [Fact]
    public Task DeserializeSegment()
    {
        var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Model", "segments.json"));
        var segments = JsonSerializer.Deserialize<Segment[]>(json, ReusableJsonSerializerOptions.Web);

        Assert.Equal(2, segments.Length);

        return Verify(segments);
    }
}