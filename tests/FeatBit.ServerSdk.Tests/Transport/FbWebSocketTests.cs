using System.Buffers;
using System.Net.WebSockets;
using System.Text;

namespace FeatBit.Sdk.Server.Transport;

[Collection(nameof(TestApp))]
public class FbWebSocketTests
{
    private readonly TestApp _app;

    public FbWebSocketTests(TestApp app)
    {
        _app = app;
    }

    [Fact]
    public async Task TestOnConnected()
    {
        var fbWebSocket = _app.CreateFbWebSocket("echo");

        var onConnectedCalled = false;

        fbWebSocket.OnConnected += () =>
        {
            onConnectedCalled = true;
            return Task.CompletedTask;
        };

        await fbWebSocket.StartAsync();
        Assert.True(onConnectedCalled);
    }

    [Fact]
    public async Task TestOnConnectError()
    {
        var fbWebSocket = _app.CreateFbWebSocket(
            "echo",
            builder => builder.Steaming(new Uri("ws://localhost/not-found"))
        );

        var onConnectErrorCalled = false;
        fbWebSocket.OnConnectError += ex =>
        {
            Assert.NotNull(ex);
            onConnectErrorCalled = true;
        };

        await fbWebSocket.StartAsync();

        Assert.True(onConnectErrorCalled);
    }

    [Fact]
    public async Task TestOnReceived()
    {
        var fbWebSocket = _app.CreateFbWebSocket("echo");

        var send = Encoding.UTF8.GetBytes("hello world");

        var tcs = new TaskCompletionSource();
        var receiveTask = tcs.Task;

        var received = Array.Empty<byte>();
        fbWebSocket.OnReceived += bytes =>
        {
            received = bytes.ToArray();
            tcs.SetResult();
            return Task.CompletedTask;
        };

        await fbWebSocket.StartAsync();
        await fbWebSocket.SendAsync(send);

        await receiveTask.WaitAsync(TimeSpan.FromMilliseconds(500));

        Assert.Equal(send, received);
    }

    [Fact]
    public async Task TestOnClosed_ClientInitiated()
    {
        var fbWebSocket = _app.CreateFbWebSocket("echo");

        var onClosedCalled = false;
        fbWebSocket.OnClosed += (exception, closeStatus, closeDescription) =>
        {
            Assert.Null(exception);
            Assert.Equal(WebSocketCloseStatus.NormalClosure, closeStatus);
            Assert.Equal(string.Empty, closeDescription);

            onClosedCalled = true;
            return Task.CompletedTask;
        };

        await fbWebSocket.StartAsync();
        await fbWebSocket.StopAsync();

        Assert.True(onClosedCalled);
    }

    [Theory]
    [InlineData("close-normally", WebSocketCloseStatus.NormalClosure, "")]
    [InlineData("close-with-4003", (WebSocketCloseStatus)4003, "invalid request, close by server")]
    public async Task TestOnClosed_ServerInitiated(
        string op,
        WebSocketCloseStatus expectCloseStatus,
        string expectCloseDescription)
    {
        var fbWebSocket = _app.CreateFbWebSocket(op);

        var tcs = new TaskCompletionSource();
        var onClosedTask = tcs.Task;
        fbWebSocket.OnClosed += (exception, closeStatus, closeDescription) =>
        {
            Assert.Null(exception);
            Assert.Equal(expectCloseStatus, closeStatus);
            Assert.Equal(expectCloseDescription, closeDescription);

            tcs.SetResult();
            return Task.CompletedTask;
        };

        await fbWebSocket.StartAsync();
        await onClosedTask.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task TestOnKeepAlive()
    {
        var fbWebSocket = _app.CreateFbWebSocket(
            "echo",
            builder => builder.KeepAliveInterval(TimeSpan.FromMilliseconds(50))
        );

        var keepAliveTimes = 0;
        fbWebSocket.OnKeepAlive += () =>
        {
            keepAliveTimes++;
            return Task.CompletedTask;
        };

        await fbWebSocket.StartAsync();
        await Task.Delay(120);

        Assert.Equal(2, keepAliveTimes);
    }
}