using FeatBit.Sdk.Server.Options;
using FeatBit.Sdk.Server.Transport;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace FeatBit.Sdk.Server;

public class TestApp : WebApplicationFactory<TestStartup>
{
    internal Uri GetWsUri(string op)
    {
        var serverUrl = Server.BaseAddress;
        var wsUri = new UriBuilder(serverUrl)
        {
            Scheme = "ws",
            Path = "ws",
            Query = $"?op={op}"
        }.Uri;

        return wsUri;
    }

    internal WebSocketTransport CreateWebSocketTransport()
    {
        var client = Server.CreateWebSocketClient();

        return new WebSocketTransport(
            webSocketFactory: (uri, cancellationToken) => client.ConnectAsync(uri, cancellationToken)
        );
    }

    internal FbWebSocket CreateFbWebSocket(string op, Func<FbOptionsBuilder, FbOptionsBuilder> configure = null)
    {
        var builder = new FbOptionsBuilder("fake-env-secret");
        configure?.Invoke(builder);
        var options = builder.Build();

        Uri WebsocketUriResolver(FbOptions ops)
        {
            return
                // if not default streaming uri, then use user defined uri
                ops.StreamingUri.OriginalString != "ws://localhost:5100"
                    ? ops.StreamingUri
                    : GetWsUri(op);
        }

        return new FbWebSocket(options, CreateWebSocketTransport, WebsocketUriResolver);
    }

    internal FbWebSocket CreateFbWebSocket(FbOptions options)
    {
        return new FbWebSocket(options, CreateWebSocketTransport);
    }

    protected override TestServer CreateServer(IWebHostBuilder builder) =>
        base.CreateServer(builder.UseSolutionRelativeContentRoot(""));

    protected override IWebHostBuilder CreateWebHostBuilder() =>
        WebHost.CreateDefaultBuilder().UseStartup<TestStartup>();
}