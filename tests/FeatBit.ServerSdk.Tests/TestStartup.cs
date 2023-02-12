using System.Net.WebSockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace FeatBit.Sdk.Server;

public class TestStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app)
    {
        app.UseWebSockets();
        app.Use(async (context, next) =>
        {
            var requestPath = context.Request.Path;
            if (requestPath.StartsWithSegments("/ws"))
            {
                var query = context.Request.Query;
                query.TryGetValue("op", out var op);
                if (context.WebSockets.IsWebSocketRequest)
                {
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    await HandleAsync(webSocket, op);
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

    private static async Task HandleAsync(WebSocket webSocket, string op)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        if (op == "echo")
        {
            await Echo(webSocket);
        }

        if (op == "close-normally")
        {
            await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, string.Empty, cts.Token);
        }

        if (op == "close-with-4003")
        {
            await webSocket.CloseOutputAsync(
                (WebSocketCloseStatus)4003,
                "invalid request, close by server",
                cts.Token
            );
        }

        if (op == "close-unexpectedly")
        {
            await webSocket.CloseOutputAsync(
                WebSocketCloseStatus.EndpointUnavailable,
                "server going down",
                cts.Token
            );
        }
    }

    private static async Task Echo(WebSocket webSocket)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        var buffer = new byte[1024 * 4];
        var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        while (!receiveResult.CloseStatus.HasValue)
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(buffer, 0, receiveResult.Count),
                receiveResult.MessageType,
                receiveResult.EndOfMessage,
                cts.Token
            );

            receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            cts.Token
        );
    }
}