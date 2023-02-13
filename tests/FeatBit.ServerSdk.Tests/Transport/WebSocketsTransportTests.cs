using System.Net.WebSockets;
using System.Text;

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
        var uri = _app.GetWsUri("echo");

        await transport.StartAsync(uri);

        Assert.Equal(WebSocketState.Open, transport.State);

        await Task.Delay(100);

        await transport.StopAsync();
        Assert.Equal(WebSocketState.Closed, transport.State);
    }

    [Fact]
    public async Task SendReceiveAsync()
    {
        var transport = _app.CreateWebSocketTransport();
        var uri = _app.GetWsUri("echo");

        await transport.StartAsync(uri);

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