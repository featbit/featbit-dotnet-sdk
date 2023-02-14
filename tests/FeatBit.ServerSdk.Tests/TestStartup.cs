using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FeatBit.Sdk.Server;

public class TestStartup : StartupBase
{
    private readonly byte[] _fullDataSet;
    private readonly byte[] _patchDataSet;

    public TestStartup()
    {
        _fullDataSet = Encoding.UTF8.GetBytes(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"DataSynchronizer\full-data-set.json"))
        );

        _patchDataSet = Encoding.UTF8.GetBytes(
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, @"DataSynchronizer\patch-data-set.json"))
        );
    }

    public override void Configure(IApplicationBuilder app)
    {
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            var requestPath = context.Request.Path;
            var query = context.Request.Query;

            if (context.WebSockets.IsWebSocketRequest)
            {
                if (requestPath.StartsWithSegments("/ws"))
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    query.TryGetValue("op", out var op);

                    await HandleOpAsync(webSocket, op);
                }
                else if (requestPath.StartsWithSegments("/streaming"))
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    query.TryGetValue("type", out var type);
                    query.TryGetValue("token", out var token);

                    await HandleStreamingAsync(webSocket, type, token);
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                }
            }
            else
            {
                await next();
            }
        });
    }

    private static async Task HandleOpAsync(WebSocket webSocket, string op)
    {
        if (op == "echo")
        {
            await Echo(webSocket);
        }

        if (op == "close-normally")
        {
            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);
        }

        if (op == "close-with-4003")
        {
            await webSocket.CloseOutputAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                CancellationToken.None
            );
        }

        if (op == "close-unexpectedly")
        {
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "server going down",
                CancellationToken.None
            );
        }
    }

    private async Task HandleStreamingAsync(WebSocket webSocket, string type, string token)
    {
        if (type != "server" || string.IsNullOrWhiteSpace(token))
        {
            await webSocket.CloseAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                CancellationToken.None
            );
        }

        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        while (!receiveResult.CloseStatus.HasValue)
        {
            var message = buffer[..receiveResult.Count];
            try
            {
                using var jsonDocument = JsonDocument.Parse(message);
                var root = jsonDocument.RootElement;

                var messageType = root.GetProperty("messageType").GetString();
                if (messageType == "data-sync")
                {
                    var timestamp = root.GetProperty("data").GetProperty("timestamp").GetInt64();
                    var response = timestamp == 0 ? _fullDataSet : _patchDataSet;

                    await webSocket.SendAsync(response, WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
            catch
            {
                // handle streaming message error
            }

            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None
        );
    }

    private static async Task Echo(WebSocket webSocket)
    {
        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                CancellationToken.None
            );

            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None
        );
    }
}