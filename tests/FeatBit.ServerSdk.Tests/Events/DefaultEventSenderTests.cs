using System.Net;
using System.Text;
using FeatBit.Sdk.Server.Options;

namespace FeatBit.Sdk.Server.Events;

public class DefaultEventSenderTests
{
    [Theory]
    // ok
    [InlineData(HttpStatusCode.OK, DeliveryStatus.Succeeded)]

    // recoverable error
    [InlineData(HttpStatusCode.TooManyRequests, DeliveryStatus.Failed)]
    [InlineData(HttpStatusCode.RequestTimeout, DeliveryStatus.Failed)]

    // rejected: delivery pauses
    [InlineData(HttpStatusCode.NotFound, DeliveryStatus.Rejected)]
    [InlineData(HttpStatusCode.Unauthorized, DeliveryStatus.Rejected)]

    // batch must be split
    [InlineData(HttpStatusCode.RequestEntityTooLarge, DeliveryStatus.PayloadTooLarge)]
    internal async Task CheckDeliveryStatusBasedOnServerReturns(HttpStatusCode code, DeliveryStatus status)
    {
        var options = new FbOptionsBuilder("secret").Build();

        var httpClient = new HttpClient(new EventHttpMessageHandlerMock(SequencedCode));
        var sender = new DefaultEventSender(options, httpClient);

        var payload = Encoding.UTF8.GetBytes("{ \"value\": 1 }");
        var deliveryStatus = await sender.SendAsync(payload);

        Assert.Equal(status, deliveryStatus);

        HttpStatusCode SequencedCode(int _) => code;
    }

    [Fact]
    public async Task ReturnsOkAfterRetry()
    {
        var options = new FbOptionsBuilder("secret")
            .SendEventRetryInterval(TimeSpan.FromMilliseconds(5))
            .Build();

        var httpClient = new HttpClient(new EventHttpMessageHandlerMock(SequencedCode));
        var sender = new DefaultEventSender(options, httpClient);

        var payload = Encoding.UTF8.GetBytes("{ \"value\": 1 }");
        var deliveryStatus = await sender.SendAsync(payload);

        Assert.Equal(DeliveryStatus.Succeeded, deliveryStatus);

        HttpStatusCode SequencedCode(int sequence) =>
            sequence % 2 == 0 ? HttpStatusCode.RequestTimeout : HttpStatusCode.OK;
    }

    [Fact]
    public async Task RetriesWhenAnAttemptTimesOut()
    {
        var options = new FbOptionsBuilder("secret")
            .SendEventTimeout(TimeSpan.FromMilliseconds(200))
            .SendEventRetryInterval(TimeSpan.FromMilliseconds(5))
            .Build();

        // first attempt hangs past the timeout, second responds immediately
        var handler = new EventHttpMessageHandlerMock(_ => HttpStatusCode.OK, delayFirst: TimeSpan.FromSeconds(5));
        var sender = new DefaultEventSender(options, new HttpClient(handler));

        var deliveryStatus = await sender.SendAsync(Encoding.UTF8.GetBytes("{ \"value\": 1 }"));

        Assert.Equal(DeliveryStatus.Succeeded, deliveryStatus);
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task FailsAfterEveryAttemptTimesOut()
    {
        var options = new FbOptionsBuilder("secret")
            .SendEventTimeout(TimeSpan.FromMilliseconds(100))
            .SendEventRetryInterval(TimeSpan.FromMilliseconds(5))
            .MaxSendEventAttempts(3)
            .Build();

        var handler = new EventHttpMessageHandlerMock(_ => HttpStatusCode.OK, delayAll: TimeSpan.FromSeconds(5));
        var sender = new DefaultEventSender(options, new HttpClient(handler));

        var deliveryStatus = await sender.SendAsync(Encoding.UTF8.GetBytes("{ \"value\": 1 }"));

        Assert.Equal(DeliveryStatus.Failed, deliveryStatus);
        Assert.Equal(3, handler.Calls);
    }
}

internal sealed class EventHttpMessageHandlerMock : HttpMessageHandler
{
    private int _sequence;
    private readonly Func<int, HttpStatusCode> _sequencedCode;
    private readonly TimeSpan _delayFirst;
    private readonly TimeSpan _delayAll;

    public int Calls => _sequence;

    public EventHttpMessageHandlerMock(
        Func<int, HttpStatusCode> sequencedCode,
        TimeSpan delayFirst = default,
        TimeSpan delayAll = default)
    {
        _sequencedCode = sequencedCode;
        _sequence = 0;
        _delayFirst = delayFirst;
        _delayAll = delayAll;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Assert.Equal("secret", request.Headers.Authorization?.Scheme);
        Assert.Equal(HttpMethod.Post, request.Method);

        var sequence = _sequence++;
        var delay = sequence == 0 && _delayFirst > TimeSpan.Zero ? _delayFirst : _delayAll;
        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, cancellationToken);
        }

        return new HttpResponseMessage(_sequencedCode(sequence));
    }
}