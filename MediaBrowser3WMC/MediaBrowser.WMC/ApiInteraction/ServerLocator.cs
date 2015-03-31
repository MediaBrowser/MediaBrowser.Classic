using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MediaBrowser.Model.ApiClient;
using Newtonsoft.Json;

namespace MediaBrowser.ApiInteraction
{
    public class ServerLocator
    {
        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        public ServerInfo FindServer()
        {
            // Create a udp client
            var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort()));
            client.Client.ReceiveTimeout = 5000;

            // Construct the message the server is expecting
            var bytes = Encoding.UTF8.GetBytes("who is EmbyServer?");

            // Send it - must be IPAddress.Broadcast, 7359
            var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

            // Send it
            client.Send(bytes, bytes.Length, targetEndPoint);

            // Get a result back
            try
            {
                var result = client.Receive(ref targetEndPoint);

                // Convert bytes to text
                var json = Encoding.UTF8.GetString(result);

                var info = new NewtonsoftJsonSerializer().DeserializeFromString<ServerDiscoveryInfo>(json);

                return new ServerInfo
                       {
                           Name = info.Name,
                           Id = info.Id,
                           LocalAddress = info.Address
                       };
            }
            catch (Exception exception)
            {
                // We'll return null
            }
            return null;
        }

        /// <summary>
        /// Gets a random port number that is currently available
        /// </summary>
        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Any, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
