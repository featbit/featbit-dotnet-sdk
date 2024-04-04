namespace FeatBit.Sdk.Server.Concurrent;

public class StatusManagerTests
{
    public enum TestStatus
    {
        A,
        B
    }

    [Fact]
    public void SetInitialStatus()
    {
        var statusManager = new StatusManager<TestStatus>(TestStatus.A);
        Assert.Equal(TestStatus.A, statusManager.Status);
    }

    [Fact]
    public void ChangesStatus()
    {
        var statusManager = new StatusManager<TestStatus>(TestStatus.A);
        statusManager.SetStatus(TestStatus.B);
        Assert.Equal(TestStatus.B, statusManager.Status);
    }

    [Theory]
    [InlineData(TestStatus.A, TestStatus.B, true)]
    [InlineData(TestStatus.A, TestStatus.A, false)]
    public void TriggerIfStatusChangedEvent(TestStatus old, TestStatus @new, bool changed)
    {
        var eventTriggered = false;
        var statusManager = new StatusManager<TestStatus>(old, newStatus =>
        {
            Assert.Equal(newStatus, @new);
            eventTriggered = true;
        });
        statusManager.SetStatus(@new);

        Assert.Equal(changed, eventTriggered);
    }

    [Theory]
    [InlineData(TestStatus.A, TestStatus.B, true)]
    [InlineData(TestStatus.B, TestStatus.A, false)]
    public void CompareAndSetStatus(TestStatus expected, TestStatus newStatus, bool setSuccess)
    {
        var statusManager = new StatusManager<TestStatus>(TestStatus.A);
        var compareAndSetSuccess = statusManager.CompareAndSet(expected, newStatus);

        Assert.Equal(setSuccess, compareAndSetSuccess);
        Assert.Equal(newStatus, statusManager.Status);
    }
}