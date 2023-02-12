namespace FeatBit.Sdk.Server.Retry;

public class DefaultRetryPolicyTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(3, 8)]
    // "restart"
    [InlineData(4, 2)]
    [InlineData(5, 3)]
    [InlineData(6, 5)]
    [InlineData(7, 8)]
    public void GetNextRetryDelay(int attempt, int seconds)
    {
        var delays = new[] { 2, 3, 5, 8 }
            .Select(x => TimeSpan.FromSeconds(x))
            .ToList();

        var policy = new DefaultRetryPolicy(delays);

        var retryContext = new RetryContext { RetryAttempt = attempt };
        Assert.Equal(TimeSpan.FromSeconds(seconds), policy.NextRetryDelay(retryContext));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(7)]
    public void GetConstantRetryDelay(int attempt)
    {
        var delays = new[] { TimeSpan.FromSeconds(1) };

        var policy = new DefaultRetryPolicy(delays);

        Assert.Equal(TimeSpan.FromSeconds(1), policy.NextRetryDelay(new RetryContext { RetryAttempt = attempt }));
    }
}