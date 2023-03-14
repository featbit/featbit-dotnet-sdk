using System;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events;

internal sealed partial class DefaultEventDispatcher
{
    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Start dispatch loop.")]
        public static partial void StartDispatchLoop(ILogger logger);

        [LoggerMessage(2, LogLevel.Debug, "Finish dispatch loop.")]
        public static partial void FinishDispatchLoop(ILogger logger);

        [LoggerMessage(3, LogLevel.Debug, "Added event to buffer.")]
        public static partial void AddedEventToBuffer(ILogger logger);

        [LoggerMessage(4, LogLevel.Debug, "{Count} events has been flushed.")]
        public static partial void EventsFlushed(ILogger logger, int count);

        [LoggerMessage(5, LogLevel.Debug, "Flush empty buffer.")]
        public static partial void FlushEmptyBuffer(ILogger logger);

        [LoggerMessage(6, LogLevel.Warning,
            "Exceeded event queue capacity, event will be dropped. Increase capacity to avoid dropping events.")]
        public static partial void ExceededCapacity(ILogger logger);

        [LoggerMessage(7, LogLevel.Debug,
            "The number of flush workers has reached the limit. This flush event will be skipped.")]
        public static partial void TooManyFlushWorkers(ILogger logger);

        [LoggerMessage(8, LogLevel.Error, "Unexpected error in event dispatcher thread.")]
        public static partial void DispatchError(ILogger logger, Exception ex);
    }
}