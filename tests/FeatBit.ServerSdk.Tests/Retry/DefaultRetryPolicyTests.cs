namespace FeatBit.Sdk.Server.Retry;

public class DefaultRetryPolicyTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 8)]
    [InlineData(2, 32)]
    // stays at the last delay instead of wrapping back to the first
    [InlineData(3, 32)]
    [InlineData(100, 32)]
    public void RetryDelayUsesExpectedJitterRange(int attempt, int seconds)
    {
        var delays = new[] { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(32) };
        var policy = new DefaultRetryPolicy(delays);

        var retryContext = new RetryContext { RetryAttempt = attempt };
        Assert.InRange(policy.NextRetryDelay(retryContext),
            TimeSpan.FromSeconds(seconds / 2.0), TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public void DefaultScheduleKeepsUsingLastDelayDuringALongOutage()
    {
        var policy = new DefaultRetryPolicy();
        var firstAttemptPastSchedule = DefaultRetryPolicy.DefaultRetryDelays.Length;

        Assert.InRange(policy.NextRetryDelay(new RetryContext { RetryAttempt = firstAttemptPastSchedule }),
            TimeSpan.FromSeconds(27.5), TimeSpan.FromSeconds(55));
    }

    [Fact]
    public void ZeroDelayIsNotJittered()
    {
        var policy = new DefaultRetryPolicy();

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
