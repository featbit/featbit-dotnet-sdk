using System.Threading.Tasks;

namespace FeatBit.Sdk.Server.Utils;

public static class TaskExtensions
{
    /// <summary>
    /// Observes the task to avoid the UnobservedTaskException event to be raised.
    /// </summary>
    public static void Forget(this Task task)
    {
        if (!task.IsCompleted || task.IsFaulted)
        {
            _ = ForgetAwaited(task);
        }

        return;

        static async Task ForgetAwaited(Task task)
        {
#if NET8_0_OR_GREATER
            await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
#else

            try
            {
                // No need to resume on the original SynchronizationContext, so use ConfigureAwait(false)
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Nothing to do here
            }
#endif
        }
    }
}