using System.Diagnostics;
using System.Net.WebSockets;
using FeatBit.Sdk.Server.Model;

namespace FeatBit.Sdk.Server.Resiliency;

/// <summary>
/// End-to-end connection resilience tests against a real (Kestrel + TCP) fake evaluation server.
/// Every client is closed in a finally block so a failed assertion never leaves a retry loop running.
/// </summary>
public class ConnectionResilienceTests
{
    private static readonly FbUser User = FbUser.Builder("u1").Build();

    // #47: a connection that goes silent (hung server, black-holed LB/NAT path) must be detected.
    [Fact]
    public async Task SilentConnectionIsDetectedAndReplaced()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            if (n > 1)
            {
                await els.ServeNormallyAsync(ws, n, ct);
                return;
            }

            // first connection: behave normally for a few pings, then go completely silent while
            // keeping the TCP connection open (stop reading, stop writing, never close)
            var pings = 0;
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                await els.RespondAsync(ws, type, ct);
                if (type == "ping" && ++pings == 2) break;
            }

            // flags change on the server while this client is wedged
            els.FullSyncFlag = FakeEls.Flag(value: true, version: 2);
            await Task.Delay(Timeout.Infinite, ct);
        };

        var client = new FbClient(els.Options().Builder
            .KeepAliveInterval(TimeSpan.FromMilliseconds(200))
            .KeepAliveTimeout(TimeSpan.FromMilliseconds(600))
            .Build());
        try
        {
            Assert.True(client.Initialized);

            await WaitUntilAsync(() => els.StreamingConnections >= 2 && client.BoolVariation("f", User));

            Assert.True(
                els.StreamingConnections >= 2 && client.BoolVariation("f", User),
                $"Still on the silent connection. connections={els.StreamingConnections}, Status={client.Status}"
            );
            Assert.Equal(FbClientStatus.Ready, client.Status);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #47 guard: a server that never answers pings must not be treated as dead.
    [Fact]
    public async Task ConnectionStaysOpenWhenServerNeverAnswersPings()
    {
        var pingsReceived = 0;
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                if (type == "ping")
                {
                    Interlocked.Increment(ref pingsReceived);
                    continue;
                }

                await els.RespondAsync(ws, type, ct);
            }
        };

        var client = new FbClient(els.Options().Builder
            .KeepAliveInterval(TimeSpan.FromMilliseconds(100))
            .KeepAliveTimeout(TimeSpan.FromMilliseconds(300))
            .Build());
        try
        {
            Assert.True(client.Initialized);

            // wait for enough unanswered pings that the keep-alive check has run well past the timeout
            await WaitUntilAsync(() => Volatile.Read(ref pingsReceived) >= 8);
            Assert.True(Volatile.Read(ref pingsReceived) >= 8, $"pings received: {pingsReceived}");

            Assert.Equal(1, els.StreamingConnections);
            Assert.Equal(FbClientStatus.Ready, client.Status);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #48: a 4003 after the client has synchronized is treated as transient.
    [Fact]
    public async Task SyncRecoversAfterSingle4003DuringReconnect()
    {
        var flagChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            switch (n)
            {
                case 1:
                    // healthy, then the server restarts once the test has changed the flag
                    await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
                    {
                        await els.RespondAsync(ws, type, ct);
                        if (type == "data-sync") break;
                    }

                    await flagChanged.Task.WaitAsync(ct);
                    await ws.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "restarting", ct);
                    break;
                case 2:
                    // first connection after restart is rejected
                    await ws.CloseOutputAsync((WebSocketCloseStatus)4003, "invalid request, close by server", ct);
                    break;
                default:
                    await els.ServeNormallyAsync(ws, n, ct);
                    break;
            }
        };

        var client = new FbClient(els.Options().Builder.Build());
        try
        {
            Assert.True(client.Initialized);

            // the flag changes, then the server restarts
            els.FullSyncFlag = FakeEls.Flag(value: true, version: 2);
            flagChanged.SetResult();

            await WaitUntilAsync(() => client.BoolVariation("f", User));

            Assert.True(
                client.Status == FbClientStatus.Ready && els.StreamingConnections >= 3,
                $"SDK gave up after one 4003. Status={client.Status}, connections={els.StreamingConnections}"
            );
            Assert.True(client.BoolVariation("f", User));
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #48 guard: a client that has never synchronized still gives up on 4003 (e.g. invalid secret).
    [Fact]
    public async Task RejectionBeforeFirstSyncStopsReconnecting()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = (ws, n, ct) =>
            ws.CloseOutputAsync((WebSocketCloseStatus)4003, "invalid request, close by server", ct);

        var client = new FbClient(els.Options().Builder.Build());
        try
        {
            // Closed is terminal: the client has given up and has no reconnect pending
            await WaitUntilAsync(() => client.Status == FbClientStatus.Closed);

            Assert.Equal(FbClientStatus.Closed, client.Status);
            Assert.False(client.Initialized);
            Assert.Equal(1, els.StreamingConnections);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #52: a server that accepts the socket and immediately closes it must not be hammered,
    // but the client must keep retrying.
    [Fact]
    public async Task ReconnectDelaysIncreaseWhenServerAcceptsThenCloses()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = (ws, n, ct) =>
            ws.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "service unavailable, close by server", ct);

        // default delay table: 0, 1, 2, 3, ... seconds, each jittered into [delay/2, delay]
        var stopwatch = Stopwatch.StartNew();
        var client = new FbClient(els.Options().Builder
            .ReconnectRetryDelays(Retry.DefaultRetryPolicy.DefaultRetryDelays)
            .ConnectTimeout(TimeSpan.FromMilliseconds(500))
            .StartWaitTime(TimeSpan.FromSeconds(1))
            .Build());
        try
        {
            // the retry loop must make progress: initial + immediate retry + one after <= 1s
            await WaitUntilAsync(() => els.StreamingConnections >= 3);
            Assert.True(els.StreamingConnections >= 3, $"retry loop stalled at {els.StreamingConnections} connections");

            var remaining = TimeSpan.FromSeconds(3) - stopwatch.Elapsed;
            if (remaining > TimeSpan.Zero) await Task.Delay(remaining);

            // ...but stay bounded. Without escalating delays this was ~2,900 connections in 3s.
            Assert.InRange(els.StreamingConnections, 3, 6);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    // #53: a data-sync request that gets no reply is retried on a new connection.
    [Fact]
    public async Task UnansweredInitialDataSyncIsRetried()
    {
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            var dropped = false;
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                if (n == 1 && type == "data-sync" && !dropped)
                {
                    // the server's data-sync handler failed: it logs and sends nothing
                    dropped = true;
                    continue;
                }

                await els.RespondAsync(ws, type, ct);
            }
        };

        var client = new FbClient(els.Options().Builder
            .KeepAliveInterval(TimeSpan.FromMilliseconds(200))
            .DataSyncTimeout(TimeSpan.FromMilliseconds(500))
            .ConnectTimeout(TimeSpan.FromMilliseconds(400))
            .StartWaitTime(TimeSpan.FromMilliseconds(500))
            .Build());
        try
        {
            await WaitUntilAsync(() => client.Initialized);

            Assert.True(
                client.Initialized,
                $"Never initialized after one dropped data-sync reply. Status={client.Status}, connections={els.StreamingConnections}"
            );
            Assert.Equal(2, els.StreamingConnections);
        }
        finally
        {
            await client.CloseAsync();
        }
    }

    [Fact]
    public async Task UnansweredDataSyncAfterReconnectIsRetried()
    {
        var flagChanged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var els = await FakeEls.StartAsync();
        els.OnStreaming = async (ws, n, ct) =>
        {
            if (n == 1)
            {
                await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
                {
                    await els.RespondAsync(ws, type, ct);
                    if (type == "data-sync") break;
                }

                // restart only after the test has changed the flag
                await flagChanged.Task.WaitAsync(ct);
                await ws.CloseOutputAsync(WebSocketCloseStatus.InternalServerError, "restarting", ct);
                return;
            }

            var dropped = false;
            await foreach (var type in FakeEls.ReadMessagesAsync(ws, ct))
            {
                if (n == 2 && type == "data-sync" && !dropped)
                {
                    dropped = true;
                    continue;
                }

                await els.RespondAsync(ws, type, ct);
            }
        };

        var client = new FbClient(els.Options().Builder
            .KeepAliveInterval(TimeSpan.FromMilliseconds(200))
            .DataSyncTimeout(TimeSpan.FromMilliseconds(500))
            .Build());
        try
        {
            Assert.True(client.Initialized);

            els.FullSyncFlag = FakeEls.Flag(value: true, version: 2); // changed while disconnected
            flagChanged.SetResult();

            await WaitUntilAsync(() => client.BoolVariation("f", User));

            Assert.True(
                client.BoolVariation("f", User),
                $"Changes made during the disconnect were never fetched. Status={client.Status}, connections={els.StreamingConnections}"
            );
            Assert.Equal(FbClientStatus.Ready, client.Status);
            Assert.True(els.StreamingConnections >= 3);
        }
        finally
        {
            await client.CloseAsync();
        }
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
