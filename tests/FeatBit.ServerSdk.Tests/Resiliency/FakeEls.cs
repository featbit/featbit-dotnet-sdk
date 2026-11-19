using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using FeatBit.Sdk.Server.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Resiliency;

/// <summary>
/// A minimal, real (Kestrel + TCP) stand-in for the FeatBit evaluation server, so the SDK runs its
/// production ClientWebSocket / HttpClient code paths. Protocol behaviour mirrors
/// featbit/modules/evaluation-server (DataSyncMessageHandler, PingMessageHandler, StreamingMiddleware).
/// </summary>
internal sealed class FakeEls : IAsyncDisposable
{
    public const string Secret = "qJHQTVfsZUOu1Q54RLMuIQ-JtrIvNK-k-bARYicOTNQA";

    private readonly WebApplication _app;
    private int _streamingConnections;
    private int _trackRequests;

    public int StreamingConnections => Volatile.Read(ref _streamingConnections);
    public int TrackRequests => Volatile.Read(ref _trackRequests);
    public ConcurrentQueue<string> DeliveredEventBodies { get; } = new();

    /// <summary>(socket, 1-based connection number, ct). Default: <see cref="ServeNormallyAsync"/>.</summary>
    public Func<WebSocket, int, CancellationToken, Task> OnStreaming { get; set; }

    /// <summary>(context, 1-based request number). Return true if the request was handled.</summary>
    public Func<HttpContext, int, Task<bool>> OnTrack { get; set; }

    /// <summary>The single flag ("f") served on full sync.</summary>
    public string FullSyncFlag { get; set; } = Flag(value: false, version: 1);

    public Uri BaseUri { get; private set; }

    private FakeEls(WebApplication app) => _app = app;

    public static async Task<FakeEls> StartAsync()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        var els = new FakeEls(app);

        app.UseWebSockets();
        app.Map("/streaming", async ctx =>
        {
            var n = Interlocked.Increment(ref els._streamingConnections);
            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var handler = els.OnStreaming ?? els.ServeNormallyAsync;
            try
            {
                await handler(ws, n, ctx.RequestAborted);
            }
            catch (Exception)
            {
                // connection torn down by the client; fine for a test double
            }
        });
        app.MapPost("/api/public/insight/track", async ctx =>
        {
            var n = Interlocked.Increment(ref els._trackRequests);
            if (els.OnTrack != null && await els.OnTrack(ctx, n))
            {
                return;
            }

            using var reader = new StreamReader(ctx.Request.Body);
            els.DeliveredEventBodies.Enqueue(await reader.ReadToEndAsync());
            ctx.Response.StatusCode = 200;
        });

        await app.StartAsync();
        els.BaseUri = new Uri(app.Urls.First().TrimEnd('/') + "/");
        return els;
    }

    public FbOptionsBuilderFacade Options() => new(this);

    /// <summary>Behaves like ELS: answers data-sync and ping until the client closes.</summary>
    public async Task ServeNormallyAsync(WebSocket ws, int n, CancellationToken ct)
    {
        await foreach (var messageType in ReadMessagesAsync(ws, ct))
        {
            await RespondAsync(ws, messageType, ct);
        }
    }

    public async Task RespondAsync(WebSocket ws, string messageType, CancellationToken ct)
    {
        if (messageType == "data-sync")
        {
            await SendAsync(ws, DataSync("full", FullSyncFlag), ct);
        }
        else if (messageType == "ping")
        {
            await SendAsync(ws, "{\"messageType\":\"pong\",\"data\":{}}", ct);
        }
    }

    public static async IAsyncEnumerable<string> ReadMessagesAsync(
        WebSocket ws,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (ws.State == WebSocketState.Open)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(buffer, ct);
            }
            catch
            {
                yield break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                yield break;
            }

            // a message can arrive in several frames; parse it only once it is complete
            message.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            using var doc = JsonDocument.Parse(message.ToArray());
            message.SetLength(0);
            yield return doc.RootElement.GetProperty("messageType").GetString();
        }
    }

    public static Task SendAsync(WebSocket ws, string json, CancellationToken ct) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(json), WebSocketMessageType.Text, true, ct);

    public static string DataSync(string eventType, params string[] flags) =>
        $"{{\"messageType\":\"data-sync\",\"data\":{{\"eventType\":\"{eventType}\"," +
        $"\"featureFlags\":[{string.Join(",", flags)}],\"segments\":[]}}}}";

    /// <summary>A boolean flag keyed "f" that serves <paramref name="value"/> to everyone.</summary>
    public static string Flag(bool value, int version)
    {
        var updatedAt = DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000 + version).ToString("O");
        var serve = value ? "v-true" : "v-false";
        return $$"""
                 {"id":"00000000-0000-0000-0000-000000000001","key":"f","variationType":"boolean",
                  "variations":[{"id":"v-true","value":"true"},{"id":"v-false","value":"false"}],
                  "targetUsers":[],"rules":[],"isEnabled":true,"disabledVariationId":"v-false",
                  "fallthrough":{"dispatchKey":null,"includedInExpt":false,
                                 "variations":[{"id":"{{serve}}","rollout":[0,1],"exptRollout":1}]},
                  "exptIncludeAllTargets":false,"tags":[],"isArchived":false,
                  "updatedAt":"{{updatedAt}}"}
                 """;
    }

    public async ValueTask DisposeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        await _app.StopAsync(cts.Token);
        await _app.DisposeAsync();
    }

    internal sealed class FbOptionsBuilderFacade
    {
        public FbOptionsBuilder Builder { get; }

        public FbOptionsBuilderFacade(FakeEls els)
        {
            var ws = new UriBuilder(els.BaseUri) { Scheme = "ws" }.Uri;
            Builder = new FbOptionsBuilder(Secret)
                .Streaming(ws)
                .Event(els.BaseUri)
                .StartWaitTime(TimeSpan.FromSeconds(5))
                .ReconnectRetryDelays(new[] { TimeSpan.FromMilliseconds(100) });
        }
    }
}
