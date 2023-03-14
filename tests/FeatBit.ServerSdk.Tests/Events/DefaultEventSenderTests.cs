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

    // unrecoverable error
    [InlineData(HttpStatusCode.NotFound, DeliveryStatus.FailedAndMustShutDown)]
    [InlineData(HttpStatusCode.Unauthorized, DeliveryStatus.FailedAndMustShutDown)]
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
}

internal sealed class EventHttpMessageHandlerMock : HttpMessageHandler
{
    private int _sequence;
    private readonly Func<int, HttpStatusCode> _sequencedCode;

    public EventHttpMessageHandlerMock(Func<int, HttpStatusCode> sequencedCode)
    {
        _sequencedCode = sequencedCode;
        _sequence = 0;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Assert.Equal("secret", request.Headers.Authorization?.Scheme);
        Assert.Equal(HttpMethod.Post, request.Method);

        var code = _sequencedCode(_sequence++);

        return Task.FromResult(new HttpResponseMessage(code));
    }
}