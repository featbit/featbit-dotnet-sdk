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

        await fbWebSocket.ConnectAsync();
        Assert.True(onConnectedCalled);
    }

    [Fact]
    public async Task TestConnectError()
    {
        var fbWebSocket = _app.CreateFbWebSocket(
            "echo",
            builder => builder.Steaming(new Uri("ws://localhost/not-found"))
        );

        Exception connectException = null;
        try
        {
            await fbWebSocket.ConnectAsync();
        }
        catch (Exception ex)
        {
            connectException = ex;
        }

        Assert.NotNull(connectException);
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

        await fbWebSocket.ConnectAsync();
        await fbWebSocket.SendAsync(send);

        await receiveTask.WaitAsync(TimeSpan.FromMilliseconds(1000));

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

        await fbWebSocket.ConnectAsync();
        await fbWebSocket.CloseAsync();

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

        await fbWebSocket.ConnectAsync();
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

        await fbWebSocket.ConnectAsync();
        await Task.Delay(130);

        Assert.Equal(2, keepAliveTimes);
    }

    [Fact]
    public async Task TestReconnectOnUnexpectedClose()
    {
        var fbWebSocket = _app.CreateFbWebSocket("close-unexpectedly");

        var onReconnectingCalled = false;
        fbWebSocket.OnReconnecting += ex =>
        {
            Assert.Null(ex);
            onReconnectingCalled = true;
            return Task.CompletedTask;
        };

        var tcs = new TaskCompletionSource();
        var reconnectTask = tcs.Task;
        var onReconnectedCalled = false;
        fbWebSocket.OnReconnected += () =>
        {
            onReconnectedCalled = true;

            tcs.SetResult();
            return Task.CompletedTask;
        };

        await fbWebSocket.ConnectAsync();
        await reconnectTask.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(onReconnectingCalled);
        Assert.True(onReconnectedCalled);
    }

    [Fact]
    public async Task CanStopFbWebSocketWhenReconnecting()
    {
        var fbWebSocket = _app.CreateFbWebSocket(
            "close-unexpectedly",
            // set reconnect delay as 1s
            builder => builder.ReconnectRetryDelays(new[] { TimeSpan.FromSeconds(1) })
        );

        var tcs = new TaskCompletionSource();
        var onClosedTask = tcs.Task;
        fbWebSocket.OnClosed += (exception, _, _) =>
        {
            Assert.NotNull(exception);
            Assert.Equal("FbWebSocket stopped during reconnect delay. Done reconnecting.", exception.Message);

            tcs.SetResult();
            return Task.CompletedTask;
        };

        await fbWebSocket.ConnectAsync();
        await fbWebSocket.CloseAsync();

        await onClosedTask.WaitAsync(TimeSpan.FromSeconds(1));
    }
}