namespace FeatBit.Sdk.Server.Retry;

public class DefaultRetryPolicyTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 3)]
    [InlineData(2, 5)]
    [InlineData(3, 8)]
    // stays at the last delay instead of wrapping back to the first
    [InlineData(4, 8)]
    [InlineData(5, 8)]
    [InlineData(100, 8)]
    public void GetNextRetryDelay(int attempt, int seconds)
    {
        var delays = new[] { 2, 3, 5, 8 }
            .Select(x => TimeSpan.FromSeconds(x))
            .ToList();

        var policy = new DefaultRetryPolicy(delays, jitter: false);

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

        var policy = new DefaultRetryPolicy(delays, jitter: false);

        Assert.Equal(TimeSpan.FromSeconds(1), policy.NextRetryDelay(new RetryContext { RetryAttempt = attempt }));
    }

    [Fact]
    public void DefaultScheduleNeverDropsBackDuringALongOutage()
    {
        var policy = new DefaultRetryPolicy(DefaultRetryPolicy.DefaultRetryDelays, jitter: false);

        var delays = Enumerable.Range(0, 50)
            .Select(i => policy.NextRetryDelay(new RetryContext { RetryAttempt = i }))
            .ToArray();

        for (var i = 1; i < delays.Length; i++)
        {
            Assert.True(delays[i] >= delays[i - 1], $"attempt {i} waits {delays[i]} < attempt {i - 1} {delays[i - 1]}");
        }

        Assert.Equal(TimeSpan.FromSeconds(55), delays[^1]);
    }

    [Fact]
    public void JitteredDelayStaysWithinHalfToFullDelay()
    {
        var policy = new DefaultRetryPolicy(new[] { TimeSpan.FromSeconds(10) });

        for (var i = 0; i < 1000; i++)
        {
            var delay = policy.NextRetryDelay(new RetryContext { RetryAttempt = i });
            Assert.InRange(delay, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void ZeroDelayIsNotJittered()
    {
        var policy = new DefaultRetryPolicy(new[] { TimeSpan.Zero });

        Assert.Equal(TimeSpan.Zero, policy.NextRetryDelay(new RetryContext { RetryAttempt = 0 }));
    }

    [Fact]
    public void SeparateClientsDoNotReconnectInLockstep()
    {
        var a = new DefaultRetryPolicy();
        var b = new DefaultRetryPolicy();

        var sa = Enumerable.Range(1, 10).Select(i => a.NextRetryDelay(new RetryContext { RetryAttempt = i }));
        var sb = Enumerable.Range(1, 10).Select(i => b.NextRetryDelay(new RetryContext { RetryAttempt = i }));

        Assert.False(sa.SequenceEqual(sb));
    }
}
