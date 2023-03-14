using System.Text;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Events;

[UsesVerify]
public class DefaultEventSerializerTests
{
    [Fact]
    public async Task SerializeEvalEvent()
    {
        var serializer = new DefaultEventSerializer();

        var events = AllEvents();
        var result = new ReadOnlyMemory<IEvent>(events, 1, 2);

        var jsonBytes = serializer.Serialize(result);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    private static IEvent[] AllEvents()
    {
        var user1 = FbUser.Builder("u1-Id")
            .Name("u1-name")
            .Custom("custom", "value")
            .Custom("country", "us")
            .Build();
        var v1Variation = new Variation
        {
            Id = "v1Id",
            Value = "v1"
        };
        var event1 = new EvalEvent(user1, "hello", v1Variation, true);

        var user2 = FbUser.Builder("u2-Id")
            .Name("u2-name")
            .Custom("age", "10")
            .Build();
        var v2Variation = new Variation
        {
            Id = "v2Id",
            Value = "v2"
        };
        var event2 = new EvalEvent(user2, "hello", v2Variation, false);

        var user3 = FbUser.Builder("u3-Id")
            .Name("u3-name")
            .Custom("age", "10")
            .Build();
        var v3Variation = new Variation
        {
            Id = "v3Id",
            Value = "v3"
        };
        var event3 = new EvalEvent(user3, "hello", v3Variation, true);

        return new IEvent[] { event1, event2, event3 };
    }
}