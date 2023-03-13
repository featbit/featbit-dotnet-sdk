using System;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Events
{
    internal partial class DefaultEventSender
    {
        private static partial class Log
        {
            [LoggerMessage(1, LogLevel.Debug, "Start send event: {Body}")]
            public static partial void SendStarted(ILogger logger, string body);

            [LoggerMessage(2, LogLevel.Debug, "Event delivery took {ElapsedMs} ms, response status {Status}.")]
            public static partial void SendFinished(ILogger logger, long elapsedMs, int status);

            [LoggerMessage(3, LogLevel.Debug, "Event sending task was cancelled due to a handle timeout.")]
            public static partial void SendTaskWasCanceled(ILogger logger);

            [LoggerMessage(4, LogLevel.Debug, "Exception occurred when sending event.")]
            public static partial void ErrorSendEvent(ILogger logger, Exception ex);

            [LoggerMessage(5, LogLevel.Warning, "Send event failed: {Reason}.")]
            public static partial void SendFailed(ILogger logger, string reason);
        }
    }
}