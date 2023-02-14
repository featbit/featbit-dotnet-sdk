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
        return new WebSocketTransport((uri, cancellationToken) => client.ConnectAsync(uri, cancellationToken));
    }

    internal FbWebSocket CreateFbWebSocket(string op, Func<FbOptionsBuilder, FbOptionsBuilder> configure = null)
    {
        var wsUri = GetWsUri(op);

        var transport = CreateWebSocketTransport();

        var builder = new FbOptionsBuilder().Steaming(wsUri);
        configure?.Invoke(builder);
        var options = builder.Build();

        return new FbWebSocket(options, transport);
    }

    internal FbWebSocket CreateFbWebSocket(FbOptions options)
    {
        var transport = CreateWebSocketTransport();

        return new FbWebSocket(options, transport);
    }

    protected override TestServer CreateServer(IWebHostBuilder builder) =>
        base.CreateServer(builder.UseSolutionRelativeContentRoot(""));

    protected override IWebHostBuilder CreateWebHostBuilder() =>
        WebHost.CreateDefaultBuilder().UseStartup<TestStartup>();
}