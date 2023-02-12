using Xunit.Abstractions;

namespace FeatBit.Sdk.Server.Retry;

public class BackoffAndJitterRetryPolicyTests
{
    private readonly ITestOutputHelper _output;

    public BackoffAndJitterRetryPolicyTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Verify manually")]
    public async Task GetNextDelaySeries()
    {
        var min = TimeSpan.FromSeconds(1);
        var max = TimeSpan.FromSeconds(60);
        BackoffAndJitterRetryPolicy retryPolicy = new(min, max);

        for (var i = 0; i < 10; i++)
        {
            var delay = retryPolicy.NextRetryDelay(new RetryContext { RetryAttempt = i });
            await Task.Delay(1000);
            _output.WriteLine(delay.ToString());
        }
    }
}