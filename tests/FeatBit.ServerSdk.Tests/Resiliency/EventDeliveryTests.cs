using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Resiliency;

public class EventDeliveryTests
{
    private static readonly FbUser User = FbUser.Builder("u1").Build();

    // #50: one 4xx used to disable event delivery for the lifetime of the process.
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task One_rejection_pauses_delivery_instead_of_disabling_it(int status)
    {
        await using var els = await FakeEls.StartAsync();
        els.OnTrack = (ctx, n) =>
        {
            if (n != 1) return Task.FromResult(false);

            ctx.Response.StatusCode = status;
            return Task.FromResult(true);
        };

        var options = els.Options().Builder.Build();
        options.EventRejectionBackoff = TimeSpan.FromMilliseconds(300);
        var client = new FbClient(options);
        Assert.True(client.Initialized);

        client.Track(User, "first");
        client.FlushAndWait(TimeSpan.FromSeconds(5)); // rejected

        // tracked during the pause: buffered, then delivered once the pause ends
        client.Track(User, "later");
        await Task.Delay(400);
        client.FlushAndWait(TimeSpan.FromSeconds(5));

        try
        {
            Assert.True(
                els.DeliveredEventBodies.Any(b => b.Contains("later")),
                $"No events delivered after one HTTP {status}. track requests={els.TrackRequests}"
            );
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #50: a batch rejected as too large is split instead of disabling delivery.
    [Fact]
    public async Task Payload_too_large_is_split_and_delivered()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnTrack = async (ctx, n) =>
        {
            using var reader = new StreamReader(ctx.Request.Body);
            var body = await reader.ReadToEndAsync();

            // accept at most 2 events per request
            if (body.Split("\"eventName\"").Length - 1 > 2)
            {
                ctx.Response.StatusCode = 413;
                return true;
            }

            els.DeliveredEventBodies.Enqueue(body);
            ctx.Response.StatusCode = 200;
            return true;
        };

        var client = new FbClient(els.Options().Builder.Build());
        Assert.True(client.Initialized);

        for (var i = 0; i < 4; i++) client.Track(User, $"metric-{i}");
        client.FlushAndWait(TimeSpan.FromSeconds(5));

        try
        {
            // 1 rejected 4-event request, then two accepted 2-event halves
            Assert.Equal(3, els.TrackRequests);
            Assert.Equal(2, els.DeliveredEventBodies.Count);

            var delivered = string.Join("", els.DeliveredEventBodies);
            for (var i = 0; i < 4; i++)
            {
                Assert.Contains($"metric-{i}", delivered);
            }
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #51: an attempt that exceeds the timeout used to drop the batch without retrying.
    [Fact]
    public async Task Slow_event_endpoint_is_retried_instead_of_dropping_the_batch()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnTrack = async (ctx, n) =>
        {
            if (n != 1) return false;

            // first request is slower than the send timeout
            await Task.Delay(2500, ctx.RequestAborted);
            return false;
        };

        var client = new FbClient(els.Options().Builder.SendEventTimeout(TimeSpan.FromSeconds(1)).Build());
        Assert.True(client.Initialized);

        client.Track(User, "checkout");
        client.FlushAndWait(TimeSpan.FromSeconds(10));

        try
        {
            Assert.True(
                els.DeliveredEventBodies.Any(b => b.Contains("checkout")),
                $"Event lost. track requests={els.TrackRequests}, delivered={els.DeliveredEventBodies.Count}"
            );
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #51: the default timeout tolerates a response that takes longer than the old 2s limit.
    [Fact]
    public async Task Default_timeout_tolerates_a_2_5s_response()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnTrack = async (ctx, n) =>
        {
            await Task.Delay(2500, ctx.RequestAborted);
            return false;
        };

        var client = new FbClient(els.Options().Builder.Build());
        Assert.True(client.Initialized);

        client.Track(User, "checkout");
        client.FlushAndWait(TimeSpan.FromSeconds(10));

        try
        {
            Assert.Contains(els.DeliveredEventBodies, b => b.Contains("checkout"));
            Assert.Equal(1, els.TrackRequests);
        }
        finally
        {
            await client.CloseAsync();
        }
    }
}
