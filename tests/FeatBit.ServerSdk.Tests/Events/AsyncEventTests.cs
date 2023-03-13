namespace FeatBit.Sdk.Server.Events;

public class AsyncEventTests
{
    [Theory]
    [ClassData(typeof(AsyncEvents))]
    internal void AsyncEventIsCompleted(AsyncEvent asyncEvent)
    {
        Assert.False(asyncEvent.IsCompleted);

        asyncEvent.Complete();

        Assert.True(asyncEvent.IsCompleted);
    }

    [Fact]
    internal void WaitForCompletion()
    {
        Assert.True(CompleteInTime(new FlushEvent(), 10, 100));
        Assert.False(CompleteInTime(new ShutdownEvent(), 100, 10));
    }

    [Fact]
    internal async Task WaitForCompletionAsync()
    {
        Assert.True(await CompleteInTimeAsync(new FlushEvent(), 10, 100));
        Assert.False(await CompleteInTimeAsync(new ShutdownEvent(), 100, 10));
    }

    private static bool CompleteInTime(AsyncEvent asyncEvent, int timeToComplete, int timeout)
    {
        _ = Task.Delay(timeToComplete).ContinueWith(_ => asyncEvent.Complete());
        return asyncEvent.WaitForCompletion(TimeSpan.FromMilliseconds(timeout));
    }

    private static async Task<bool> CompleteInTimeAsync(AsyncEvent asyncEvent, int timeToComplete, int timeout)
    {
        _ = Task.Delay(timeToComplete).ContinueWith(_ => asyncEvent.Complete());
        return await asyncEvent.WaitForCompletionAsync(TimeSpan.FromMilliseconds(timeout));
    }
}

internal class AsyncEvents : TheoryData<AsyncEvent>
{
    public AsyncEvents()
    {
        Add(new FlushEvent());
        Add(new ShutdownEvent());
    }
}