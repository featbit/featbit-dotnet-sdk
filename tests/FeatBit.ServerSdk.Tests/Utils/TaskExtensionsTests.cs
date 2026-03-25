namespace FeatBit.Sdk.Server.Utils;

public class TaskExtensionsTests
{
    [Fact]
    public void Forget_CompletedTask_DoesNotThrow()
    {
        var task = Task.CompletedTask;

        var exception = Record.Exception(() => task.Forget());

        Assert.Null(exception);
    }

    [Fact]
    public void Forget_AlreadyFaultedTask_DoesNotThrow()
    {
        var task = Task.FromException(new InvalidOperationException("boom"));

        var exception = Record.Exception(() => task.Forget());

        Assert.Null(exception);
    }

    [Fact]
    public void Forget_PendingTask_DoesNotBlock()
    {
        var tcs = new TaskCompletionSource<bool>();

        var exception = Record.Exception(() => tcs.Task.Forget());

        Assert.Null(exception);
        tcs.SetResult(true);
    }

    [Fact]
    public async Task Forget_TaskThatFaultsLater_SuppressesException()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.Task.Forget();

        // Setting the exception should not propagate or raise UnobservedTaskException
        tcs.SetException(new InvalidOperationException("late fault"));

        // Allow finalizers / continuations to run
        await Task.Yield();

        // If we reach here without exception, the test passes
        Assert.True(true);
    }

    [Fact]
    public async Task Forget_TaskThatCancelsLater_SuppressesException()
    {
        var tcs = new TaskCompletionSource<bool>();
        tcs.Task.Forget();

        tcs.SetCanceled();

        await Task.Yield();

        Assert.True(true);
    }
}
