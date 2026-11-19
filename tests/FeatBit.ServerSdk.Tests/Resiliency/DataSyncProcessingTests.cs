using System.Text.Json;
using FeatBit.Sdk.Server.Concurrent;
using FeatBit.Sdk.Server.DataSynchronizer;
using FeatBit.Sdk.Server.Model;
using Microsoft.Extensions.Logging.Abstractions;

namespace FeatBit.Sdk.Server.Resiliency;

public class DataSyncProcessingTests
{
    private static readonly FbUser User = FbUser.Builder("u1").Build();

    // Malformed shapes: a non-GUID id (taken from the evaluation server's own DataSyncServiceTests, which
    // deliberately tolerate such entities) and a null updatedAt.
    private const string NonGuidIdFlag =
        """{"id":"flag-1","key":"broken","variationType":"boolean","variations":[],"updatedAt":"2026-08-29T00:00:00Z"}""";

    private const string NullUpdatedAtFlag =
        """{"id":"4abf8ca8-7dc8-41d9-83ab-73a6d194e926","key":"broken","variationType":"boolean","variations":[],"updatedAt":null}""";

    // #54
    [Theory]
    [InlineData(NonGuidIdFlag)]
    [InlineData(NullUpdatedAtFlag)]
    public async Task One_malformed_flag_does_not_block_all_other_flags(string brokenFlag)
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                if (type == "data-sync")
                {
                    await FakeEls.SendAsync(ws, FakeEls.DataSync("full", FakeEls.Flag(true, 1), brokenFlag), ct);
                }
                else
                {
                    await els.RespondAsync(ws, type, ct);
                }
            }
        };

        var client = new FbClient(els.Options().Builder
            .ConnectTimeout(TimeSpan.FromSeconds(1))
            .StartWaitTime(TimeSpan.FromSeconds(2))
            .Build());

        try
        {
            Assert.True(client.Initialized, $"Status={client.Status}");
            Assert.True(client.BoolVariation("f", User));
            Assert.Equal("flag not found", client.BoolVariationDetail("broken", User).Reason);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #54
    [Fact]
    public async Task Malformed_entity_in_a_patch_does_not_drop_the_healthy_changes_with_it()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                await els.RespondAsync(ws, type, ct);
                if (type == "data-sync") break;
            }

            await Task.Delay(200, ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", NonGuidIdFlag, FakeEls.Flag(true, 2)), ct);
            await els.ServeNormallyAsync(ws, n, ct);
        };

        var client = new FbClient(els.Options().Builder.Build());
        Assert.True(client.Initialized);
        Assert.False(client.BoolVariation("f", User));

        await WaitUntilAsync(() => client.BoolVariation("f", User));

        try
        {
            Assert.True(client.BoolVariation("f", User));
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Fact]
    public void DataSet_skips_malformed_entities_and_counts_them()
    {
        var json = JsonDocument.Parse(
            FakeEls.DataSync("full", FakeEls.Flag(true, 1), NonGuidIdFlag, NullUpdatedAtFlag, "null")
        ).RootElement.GetProperty("data");

        var dataSet = DataSet.FromJsonElement(json, NullLogger.Instance);

        Assert.Equal(DataSet.Full, dataSet.EventType);
        Assert.Equal("f", Assert.Single(dataSet.FeatureFlags).Key);
        Assert.Empty(dataSet.Segments);
        Assert.Equal(3, dataSet.SkippedCount);
    }

    [Fact]
    public void DataSet_tolerates_missing_arrays()
    {
        var json = JsonDocument.Parse("""{"eventType":"patch"}""").RootElement;

        var dataSet = DataSet.FromJsonElement(json);

        Assert.Equal(DataSet.Patch, dataSet.EventType);
        Assert.Empty(dataSet.GetStorableObjects());
        Assert.Equal(0, dataSet.SkippedCount);
    }

    // #49
    [Fact]
    public async Task Slow_DataChanged_subscriber_does_not_delay_flag_updates()
    {
        var pushed = new TaskCompletionSource();
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                await els.RespondAsync(ws, type, ct);
                if (type == "data-sync") break;
            }

            await Task.Delay(300, ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(true, 2)), ct);
            await Task.Delay(100, ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(false, 3)), ct); // kill switch
            pushed.SetResult();
            await els.ServeNormallyAsync(ws, n, ct);
        };

        var client = new FbClient(els.Options().Builder.Build());
        Assert.True(client.Initialized);

        var notifications = 0;
        client.DataChanged += (_, e) =>
        {
            Interlocked.Increment(ref notifications);
            // e.g. a subscriber that refreshes a cache or calls another service
            if (e.Kind == DataChangeKind.Patch) Thread.Sleep(TimeSpan.FromSeconds(2));
        };

        await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await WaitUntilAsync(() => !client.BoolVariation("f", User), timeoutMs: 1000);

        try
        {
            Assert.False(
                client.BoolVariation("f", User),
                "Kill-switch update is queued behind a slow DataChanged handler"
            );

            // subscribers still receive every change, in order, once the slow handler returns
            await WaitUntilAsync(() => Volatile.Read(ref notifications) == 2, timeoutMs: 6000);
            Assert.Equal(2, notifications);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Fact]
    public async Task SerialDispatcher_delivers_in_order_without_blocking_the_caller()
    {
        var received = new List<int>();
        var done = new TaskCompletionSource();
        var dispatcher = new SerialDispatcher<int>(i =>
        {
            Thread.Sleep(5);
            lock (received) received.Add(i);
            if (i == 99) done.SetResult();
        }, NullLogger.Instance);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 100; i++) dispatcher.Post(i);
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 100, $"Post blocked for {sw.ElapsedMilliseconds}ms");
        await done.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(Enumerable.Range(0, 100), received);
    }

    [Fact]
    public async Task SerialDispatcher_keeps_going_after_a_handler_throws()
    {
        var done = new TaskCompletionSource<int>();
        var dispatcher = new SerialDispatcher<int>(i =>
        {
            if (i == 0) throw new InvalidOperationException("boom");
            done.SetResult(i);
        }, NullLogger.Instance);

        dispatcher.Post(0);
        dispatcher.Post(1);

        Assert.Equal(1, await done.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }
}
