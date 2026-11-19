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

    // A malformed payload shape must reject the whole message: treating it as empty would let a full sync
    // replace the store with nothing and still mark the client ready.
    [Theory]
    [InlineData("""{"eventType":"full","featureFlags":{},"segments":[]}""")]
    [InlineData("""{"eventType":"full","featureFlags":"oops","segments":[]}""")]
    [InlineData("""{"eventType":"full","featureFlags":null,"segments":[]}""")]
    [InlineData("""{"eventType":"full","featureFlags":[],"segments":{}}""")]
    [InlineData("""{"eventType":"full","segments":[]}""")]
    [InlineData("""{"eventType":"full","featureFlags":[]}""")]
    public void DataSet_rejects_a_payload_whose_arrays_are_missing_or_not_arrays(string payload)
    {
        var json = JsonDocument.Parse(payload).RootElement;

        Assert.Throws<JsonException>(() => DataSet.FromJsonElement(json));
    }

    [Fact]
    public async Task Full_sync_with_a_non_array_featureFlags_does_not_initialize_with_an_empty_store()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                if (type == "data-sync")
                {
                    await FakeEls.SendAsync(ws,
                        """{"messageType":"data-sync","data":{"eventType":"full","featureFlags":{},"segments":[]}}""",
                        ct);
                }
                else
                {
                    await els.RespondAsync(ws, type, ct);
                }
            }
        };

        var client = new FbClient(els.Options().Builder
            .ConnectTimeout(TimeSpan.FromMilliseconds(500))
            .StartWaitTime(TimeSpan.FromSeconds(1))
            .Build());

        try
        {
            Assert.False(client.Initialized);
            Assert.NotEqual(FbClientStatus.Ready, client.Status);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #49
    [Fact]
    public async Task Slow_DataChanged_subscriber_does_not_delay_flag_updates()
    {
        var subscribed = NewSignal();
        var firstHandlerStarted = NewSignal();
        var pushed = NewSignal();
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                await els.RespondAsync(ws, type, ct);
                if (type == "data-sync") break;
            }

            await subscribed.Task.WaitAsync(ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(true, 2)), ct);

            // send the kill switch only while the subscriber is busy with the first patch
            await firstHandlerStarted.Task.WaitAsync(ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(false, 3)), ct);
            pushed.SetResult();
            await els.ServeNormallyAsync(ws, n, ct);
        };

        var client = new FbClient(els.Options().Builder.Build());
        try
        {
            Assert.True(client.Initialized);

            // Count patches only: the initial Full notification is delivered asynchronously and may or may not
            // reach a subscriber attached right after construction.
            var notifications = 0;
            client.DataChanged += (_, e) =>
            {
                if (e.Kind != DataChangeKind.Patch) return;

                Interlocked.Increment(ref notifications);
                firstHandlerStarted.TrySetResult();
                // e.g. a subscriber that refreshes a cache or calls another service
                Thread.Sleep(TimeSpan.FromSeconds(2));
            };
            subscribed.SetResult();

            await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => !client.BoolVariation("f", User), timeoutMs: 1000);

            Assert.False(
                client.BoolVariation("f", User),
                "Kill-switch update is queued behind a slow DataChanged handler"
            );

            // the subscriber is still told about the later change once the slow handler returns
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

    [Fact]
    public async Task SerialDispatcher_coalesces_items_that_pile_up_behind_a_slow_handler()
    {
        var release = new ManualResetEventSlim();
        var received = new List<int>();
        var firstStarted = new TaskCompletionSource();
        var dispatcher = new SerialDispatcher<int>(i =>
        {
            firstStarted.TrySetResult();
            release.Wait();
            lock (received) received.Add(i);
        }, NullLogger.Instance, coalesce: (pending, next) => pending + next);

        dispatcher.Post(1);
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // the handler is stuck: 1000 more posts must not queue 1000 items
        for (var i = 0; i < 1000; i++) dispatcher.Post(1);
        release.Set();

        await WaitUntilAsync(() => { lock (received) return received.Count == 2; });
        await Task.Delay(100);

        lock (received)
        {
            // one in-flight item, then a single merged item carrying all 1000 posts
            Assert.Equal(new[] { 1, 1000 }, received);
        }
    }

    [Fact]
    public async Task Patches_that_pile_up_behind_a_slow_subscriber_arrive_as_one_notification()
    {
        // two patches that arrive while a subscriber is busy with the first are delivered as one notification
        var subscribed = NewSignal();
        var firstHandlerStarted = NewSignal();
        var pushed = NewSignal();
        var release = new ManualResetEventSlim();

        // the last patch uses a second flag so the test can observe that both later patches were applied
        var flagG = FakeEls.Flag(true, 4).Replace("\"key\":\"f\"", "\"key\":\"g\"");

        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                await els.RespondAsync(ws, type, ct);
                if (type == "data-sync") break;
            }

            await subscribed.Task.WaitAsync(ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(true, 2)), ct);   // handled first

            await firstHandlerStarted.Task.WaitAsync(ct);
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", FakeEls.Flag(false, 3)), ct);  // pending
            await FakeEls.SendAsync(ws, FakeEls.DataSync("patch", flagG), ct);                   // merged into it
            pushed.SetResult();
            await els.ServeNormallyAsync(ws, n, ct);
        };

        var client = new FbClient(els.Options().Builder.Build());
        var patches = new List<DataChangeEventArgs>();
        try
        {
            Assert.True(client.Initialized);
            client.DataChanged += (_, e) =>
            {
                if (e.Kind != DataChangeKind.Patch) return;

                lock (patches) patches.Add(e);
                firstHandlerStarted.TrySetResult();
                release.Wait(TimeSpan.FromSeconds(10)); // stay busy until the test has seen both later patches
            };
            subscribed.SetResult();

            // both later patches are applied while the first notification is still being handled
            await pushed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await WaitUntilAsync(() => client.BoolVariation("g", User));
            Assert.True(client.BoolVariation("g", User));
            Assert.False(client.BoolVariation("f", User));

            release.Set();
            await WaitUntilAsync(() => { lock (patches) return patches.Count >= 2; });
            await Task.Delay(200); // a third notification would show up here if the patches were not merged

            lock (patches)
            {
                Assert.Equal(2, patches.Count);
                Assert.All(patches, p => Assert.True(p.FeatureFlagsChanged));
            }
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Theory]
    [InlineData(DataChangeKind.Patch, DataChangeKind.Patch, DataChangeKind.Patch)]
    [InlineData(DataChangeKind.Full, DataChangeKind.Patch, DataChangeKind.Full)]
    [InlineData(DataChangeKind.Patch, DataChangeKind.Full, DataChangeKind.Full)]
    public void Merged_notification_is_Full_if_either_was_Full(DataChangeKind a, DataChangeKind b, DataChangeKind expected)
    {
        var merged = WebSocketDataSynchronizer.MergeDataChanges(
            new DataChangeEventArgs(a, featureFlagsChanged: true, segmentsChanged: false),
            new DataChangeEventArgs(b, featureFlagsChanged: false, segmentsChanged: true)
        );

        Assert.Equal(expected, merged.Kind);
        Assert.True(merged.FeatureFlagsChanged);
        Assert.True(merged.SegmentsChanged);
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }
}
