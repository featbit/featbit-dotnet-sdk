using System;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Transport
{
    internal sealed partial class FbWebSocket
    {
        private static partial class Log
        {
            [LoggerMessage(1, LogLevel.Debug, "Starting FbWebSocket.", EventName = "Starting")]
            public static partial void Starting(ILogger logger);

            [LoggerMessage(2, LogLevel.Debug, "Starting transport '{Transport}' with Url: {Url}.",
                EventName = "StartingTransport")]
            public static partial void StartingTransport(ILogger logger, string transport, Uri url);

            [LoggerMessage(3, LogLevel.Debug, "Error starting transport.", EventName = "ErrorStartingTransport")]
            public static partial void ErrorStartingTransport(ILogger logger, Exception ex);

            [LoggerMessage(4, LogLevel.Debug, "Starting receive loop.", EventName = "StartingReceiveLoop")]
            public static partial void StartingReceiveLoop(ILogger logger);

            [LoggerMessage(5, LogLevel.Debug, "Starting keep alive timer.", EventName = "StartingKeepAliveTimer")]
            public static partial void StartingKeepAliveTimer(ILogger logger);

            [LoggerMessage(6, LogLevel.Debug, "Invoking the {HandlerName} event handler.",
                EventName = "InvokingEventHandler")]
            public static partial void InvokingEventHandler(ILogger logger, string handlerName);

            [LoggerMessage(7, LogLevel.Information, "FbWebSocket started.", EventName = "Started")]
            public static partial void Started(ILogger logger);

            [LoggerMessage(8, LogLevel.Debug, "ReceiveLoop was canceled. Possibly because we were stopped gracefully.",
                EventName = "ReceiveLoopCanceled")]
            public static partial void ReceiveLoopCanceled(ILogger logger);

            [LoggerMessage(9, LogLevel.Debug, "Processing {MessageLength} bytes message from server.",
                EventName = "ProcessingMessage")]
            public static partial void ProcessingMessage(ILogger logger, long messageLength);

            [LoggerMessage(10, LogLevel.Trace, "The server connection was terminated with an error.",
                EventName = "ServerDisconnectedWithError")]
            public static partial void ServerDisconnectedWithError(ILogger logger, Exception ex);

            [LoggerMessage(11, LogLevel.Debug, "Receive loop ended.", EventName = "ReceiveLoopEnded")]
            public static partial void ReceiveLoopEnded(ILogger logger);

            [LoggerMessage(12, LogLevel.Debug, "Stopping transport.", EventName = "StoppingTransport")]
            public static partial void StoppingTransport(ILogger logger);

            [LoggerMessage(13, LogLevel.Error, "Error stopping transport.", EventName = "ErrorStoppingTransport")]
            public static partial void ErrorStoppingTransport(ILogger logger, Exception exception);

            [LoggerMessage(14, LogLevel.Debug, "Stopping keep alive timer.", EventName = "StoppingKeepAliveTimer")]
            public static partial void StoppingKeepAliveTimer(ILogger logger);

            [LoggerMessage(15, LogLevel.Warning, "FbWebSocket is trying to reconnect due to an exception. Flag evaluation results may be stale until reconnected.",
                EventName = "ReconnectingWithError")]
            public static partial void ReconnectingWithError(ILogger logger, Exception exception);

            [LoggerMessage(16, LogLevel.Warning, "FbWebSocket is trying to reconnect. Flag evaluation results may be stale until reconnected.",
                EventName = "Reconnecting")]
            public static partial void Reconnecting(ILogger logger);

            [LoggerMessage(17, LogLevel.Warning, "Give up reconnecting due to the close status {CloseStatus}.",
                EventName = "GiveUpReconnect")]
            public static partial void GiveUpReconnect(ILogger logger, WebSocketCloseStatus? closeStatus);

            [LoggerMessage(18, LogLevel.Information, "Reconnect attempt number {RetryTimes} will start in {RetryDelay}.",
                EventName = "AwaitingReconnectRetryDelay")]
            public static partial void AwaitingReconnectRetryDelay(ILogger logger, long retryTimes, TimeSpan retryDelay);

            [LoggerMessage(19, LogLevel.Warning, "Connection stopped during reconnect delay. Done reconnecting.",
                EventName = "ReconnectingStoppedDuringRetryDelay")]
            public static partial void ReconnectingStoppedDuringRetryDelay(ILogger logger);

            [LoggerMessage(20, LogLevel.Information,
                "FbWebSocket reconnected successfully after {RetryTimes} attempts and {ElapsedTime} elapsed.",
                EventName = "Reconnected")]
            public static partial void Reconnected(ILogger logger, long retryTimes, TimeSpan elapsedTime);

            [LoggerMessage(21, LogLevel.Debug, "Reconnect attempt failed.", EventName = "ReconnectAttemptFailed")]
            public static partial void ReconnectAttemptFailed(ILogger logger, Exception exception);

            [LoggerMessage(22, LogLevel.Warning, "Connection stopped during reconnect attempt. Done reconnecting.",
                EventName = "ReconnectingStoppedDuringReconnectAttempt")]
            public static partial void ReconnectingStoppedDuringReconnectAttempt(ILogger logger);

            [LoggerMessage(23, LogLevel.Trace, "Shutting down connection.", EventName = "ShuttingDown")]
            public static partial void ShuttingDown(ILogger logger);

            [LoggerMessage(24, LogLevel.Error, "Connection is shutting down with an error.",
                EventName = "ShuttingDownWithError")]
            public static partial void ShuttingDownWithError(ILogger logger, Exception exception);

            [LoggerMessage(25, LogLevel.Debug, "Terminating receive loop, which should cause everything to shut down",
                EventName = "TerminatingReceiveLoop")]
            public static partial void TerminatingReceiveLoop(ILogger logger);

            [LoggerMessage(26, LogLevel.Debug, "Waiting for the receive loop to terminate.",
                EventName = "WaitingForReceiveLoopToTerminate")]
            public static partial void WaitingForReceiveLoopToTerminate(ILogger logger);

            [LoggerMessage(27, LogLevel.Information, "FbWebSocket closed. Status: {CloseStatus}.", EventName = "Closed")]
            public static partial void Closed(ILogger logger, WebSocketCloseStatus? closeStatus);

            [LoggerMessage(28, LogLevel.Debug, "Failed to send message, transport state: {TransportState}", EventName = "FailedToSendMessage")]
            public static partial void FailedToSendMessage(ILogger logger, WebSocketState transportState);

            [LoggerMessage(29, LogLevel.Warning,
                "FbWebSocket closed abnormally with status: {Status}, description: {Description}.",
                EventName = "AbnormallyClosed")]
            public static partial void AbnormallyClosed(ILogger logger, WebSocketCloseStatus? status, string description);
        }
    }
}