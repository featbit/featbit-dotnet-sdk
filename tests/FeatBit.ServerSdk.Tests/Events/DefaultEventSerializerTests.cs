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

        var @event = AllEvalEvents()[0];

        var jsonBytes = serializer.Serialize(@event);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    [Fact]
    public async Task SerializeMetricEvent()
    {
        var serializer = new DefaultEventSerializer();

        var @event = AllMetricEvents()[0];

        var jsonBytes = serializer.Serialize(@event);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    [Fact]
    public async Task SerializeEvalEvents()
    {
        var serializer = new DefaultEventSerializer();

        var events = AllEvalEvents();
        var result = new ReadOnlyMemory<IEvent>(events, 1, 2);

        var jsonBytes = serializer.Serialize(result);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    [Fact]
    public async Task SerializeMetricEvents()
    {
        var serializer = new DefaultEventSerializer();

        var events = AllMetricEvents();
        var result = new ReadOnlyMemory<IEvent>(events, 0, 2);

        var jsonBytes = serializer.Serialize(result);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    [Fact]
    public async Task SerializeCombinedEvents()
    {
        var serializer = new DefaultEventSerializer();

        var events = new[] { AllEvalEvents()[0], AllMetricEvents()[0] };
        var result = new ReadOnlyMemory<IEvent>(events, 0, 2);

        var jsonBytes = serializer.Serialize(result);
        var json = Encoding.UTF8.GetString(jsonBytes);

        await VerifyJson(json).ScrubMember("timestamp");
    }

    private static IEvent[] AllEvalEvents()
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

    private static IEvent[] AllMetricEvents()
    {
        var user1 = FbUser.Builder("u1-Id")
            .Name("u1-name")
            .Custom("custom", "value")
            .Custom("country", "us")
            .Build();
        var event1 = new MetricEvent(user1, "click-button", 1.5d);

        var user2 = FbUser.Builder("u2-Id")
            .Name("u2-name")
            .Custom("age", "10")
            .Build();
        var event2 = new MetricEvent(user2, "click-button", 32.5d);

        var user3 = FbUser.Builder("u3-Id")
            .Name("u3-name")
            .Custom("age", "10")
            .Build();
        var event3 = new MetricEvent(user3, "click-button", 26.5d);

        return new IEvent[] { event1, event2, event3 };
    }
}