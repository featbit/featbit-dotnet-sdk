using System.Net.WebSockets;
using System.Text;
using FeatBit.Sdk.Server.Options;

namespace FeatBit.Sdk.Server.Transport;

[Collection(nameof(TestApp))]
public class WebSocketsTransportTests
{
    private readonly TestApp _app;

    public WebSocketsTransportTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task StartAndStopAsync()
    {
        var transport = _app.CreateWebSocketTransport();
        var options = new FbOptionsBuilder()
            .Steaming(_app.GetWsUri("echo"))
            .Build();

        await transport.StartAsync(options);

        Assert.Equal(WebSocketState.Open, transport.State);

        await Task.Delay(1000);

        await transport.StopAsync();

        Assert.Equal(WebSocketState.Closed, transport.State);
    }

    [Fact]
    public async Task SendReceiveAsync()
    {
        var transport = _app.CreateWebSocketTransport();
        var options = new FbOptionsBuilder()
            .Steaming(_app.GetWsUri("echo"))
            .Build();

        await transport.StartAsync(options);

        var sent = Encoding.UTF8.GetBytes("hello world");
        await transport.Output.WriteAsync(sent);

        var result = await transport.Input.ReadAsync();

        Assert.False(result.IsCanceled);
        Assert.False(result.IsCompleted);
        Assert.False(result.Buffer.IsEmpty);
        Assert.True(result.Buffer.IsSingleSegment);

        var received = result.Buffer.FirstSpan.ToArray();

        // received message should end with an RecordSeparator
        Assert.Equal(TextMessageFormatter.RecordSeparator, received.Last());

        var receivedContent = received.SkipLast(1);
        Assert.Equal(sent, receivedContent);

        await transport.StopAsync();
    }
}