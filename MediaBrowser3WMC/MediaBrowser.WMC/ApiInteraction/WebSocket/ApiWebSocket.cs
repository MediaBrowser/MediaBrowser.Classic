using System.Collections.Generic;
using System.Threading;
using MediaBrowser.Library.Logging;
using MediaBrowser.Library.Threading;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Serialization;
using System;

namespace MediaBrowser.ApiInteraction.WebSocket
{
    /// <summary>
    /// Class ApiWebSocket
    /// </summary>
    public class ApiWebSocket : BaseApiWebSocket
    {
        protected string HostName { get; set; }
        protected int Port { get; set; }
        protected string ClientName { get; set; }
        protected string DeviceId { get; set; }

        protected Timer KeepAliveTimer { get; set; }

        /// <summary>
        /// The _web socket
        /// </summary>
        private readonly IClientWebSocket _webSocket;

        public ApiWebSocket(IClientWebSocket webSocket)
            : base()
        {
            _webSocket = webSocket;
        }

        /// <summary>
        /// Connects the async.
        /// </summary>
        /// <param name="serverHostName">Name of the server host.</param>
        /// <param name="serverWebSocketPort">The server web socket port.</param>
        /// <param name="clientName">Name of the client.</param>
        /// <param name="deviceId">The device id.</param>
        public void Connect(string serverHostName, int serverWebSocketPort, string clientName, string deviceId)
        {
            HostName = serverHostName;
            Port = serverWebSocketPort;
            ClientName = clientName;
            DeviceId = deviceId;

            var url = GetWebSocketUrl(serverHostName, serverWebSocketPort);

            try
            {
                _webSocket.Connect(url);

                Library.Logging.Logger.ReportInfo("Connected to {0}", url);

                _webSocket.OnReceiveDelegate = OnMessageReceived;

                Async.Queue("ident", () =>
                                         {
                                             while (_webSocket.State != WebSocketState.Open)
                                             {
                                             }
                                             SendIdentification(clientName, deviceId);
                                             if (KeepAliveTimer != null) KeepAliveTimer.Dispose();
                                             KeepAliveTimer = new Timer(KeepAlive, null, 60000, 60000);
                                         } );
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error connecting to {0}", ex, url);
            }
        }

        protected void KeepAlive(object sender)
        {
            if (_webSocket.State != WebSocketState.Open)
            {
                Logger.ReportInfo("Reconnecting WebSocket which has dropped.");
                Connect(HostName, Port, ClientName, DeviceId);
            }
        }

        public void SendIdentification(string clientName, string deviceId)
        {
            Send(IdentificationMessageName, GetIdentificationMessage(clientName, deviceId));
        }

        /// <summary>
        /// Sends the async.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="messageName">Name of the message.</param>
        /// <param name="data">The data.</param>
        /// <returns>Task.</returns>
        public void Send<T>(string messageName, T data)
        {
            var bytes = GetMessageBytes(messageName, data);

            try
            {
                _webSocket.Send(bytes, Model.Net.WebSocketMessageType.Binary, true);
            }
            catch (Exception ex)
            {
                Logger.ReportException("Error sending web socket message", ex);

                throw;
            }
        }
        /// <summary>
        /// Sends the server a message indicating what is currently being viewed by the client
        /// </summary>
        /// <param name="itemType">The current item type (if any)</param>
        /// <param name="itemId">The current item id (if any)</param>
        /// <param name="itemName">The current item name (if any)</param>
        /// <param name="context">An optional, client-specific value indicating the area or section being browsed</param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public void SendContextMessage(string itemType, string itemId, string itemName, string context = "")
        {
            var vals = new List<string>
                {
                    itemType ?? string.Empty, 
                    itemId ?? string.Empty, 
                    itemName ?? string.Empty
                };

            if (!string.IsNullOrEmpty(context))
            {
                vals.Add(context);
            }

            Send("Context", string.Join("|", vals.ToArray()));
        }
    }
}
