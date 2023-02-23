using System;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;

namespace FeatBit.Sdk.Server.Transport;

internal sealed partial class WebSocketTransport
{
    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Started transport.", EventName = "StartedTransport")]
        public static partial void StartedTransport(ILogger logger);

        [LoggerMessage(2, LogLevel.Debug, "WebSocket closed by the server. Close status {CloseStatus}.",
            EventName = "WebSocketClosed")]
        public static partial void WebSocketClosed(ILogger logger, WebSocketCloseStatus? closeStatus);

        [LoggerMessage(3, LogLevel.Debug, "Message received. Type: {MessageType}, size: {Count}.",
            EventName = "MessageReceived")]
        public static partial void MessageReceived(ILogger logger, WebSocketMessageType messageType, int count);

        [LoggerMessage(4, LogLevel.Debug, "Receive loop canceled.", EventName = "ReceiveCanceled")]
        public static partial void ReceiveCanceled(ILogger logger);

        [LoggerMessage(5, LogLevel.Debug, "Receive loop stopped.", EventName = "ReceiveStopped")]
        public static partial void ReceiveStopped(ILogger logger);

        [LoggerMessage(6, LogLevel.Debug, "Received message from application. Payload size: {Count}.",
            EventName = "ReceivedFromApp")]
        public static partial void ReceivedFromApp(ILogger logger, long count);

        [LoggerMessage(7, LogLevel.Debug, "Error while sending a message.", EventName = "ErrorSendingMessage")]
        public static partial void ErrorSendingMessage(ILogger logger, Exception exception);

        [LoggerMessage(8, LogLevel.Debug, "Closing webSocket failed.", EventName = "ClosingWebSocketFailed")]
        public static partial void ClosingWebSocketFailed(ILogger logger, Exception exception);

        [LoggerMessage(9, LogLevel.Debug, "Send loop stopped.", EventName = "SendStopped")]
        public static partial void SendStopped(ILogger logger);

        [LoggerMessage(10, LogLevel.Debug, "Transport stopped.", EventName = "TransportStopped")]
        public static partial void TransportStopped(ILogger logger, Exception exception);
    }
}