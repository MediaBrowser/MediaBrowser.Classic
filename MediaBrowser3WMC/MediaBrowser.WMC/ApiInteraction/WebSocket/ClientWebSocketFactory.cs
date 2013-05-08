using MediaBrowser.Model.Logging;
using System;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ClientWebSocketFactory
    /// </summary>
    public static class ClientWebSocketFactory
    {
        /// <summary>
        /// Creates the web socket.
        /// </summary>
        /// <param name="logManager">The log manager.</param>
        /// <returns>IClientWebSocket.</returns>
        public static IClientWebSocket CreateWebSocket(ILogManager logManager)
        {
            return new WebSocket4NetClientWebSocket();
        }
    }
}
