using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MediaBrowser.ApiInteraction
{
    public class ServerLocator
    {
        /// <summary>
        /// Attemps to discover the server within a local network
        /// </summary>
        public IPEndPoint FindServer()
        {
            // Create a udp client
            var client = new UdpClient(new IPEndPoint(IPAddress.Any, GetRandomUnusedPort()));
            client.Client.ReceiveTimeout = 5000;

            // Construct the message the server is expecting
            var bytes = Encoding.UTF8.GetBytes("who is MediaBrowserServer?");

            // Send it - must be IPAddress.Broadcast, 7359
            var targetEndPoint = new IPEndPoint(IPAddress.Broadcast, 7359);

            // Send it
            client.Send(bytes, bytes.Length, targetEndPoint);

            // Get a result back
            try
            {
                var result = client.Receive(ref targetEndPoint);

                // Convert bytes to text
                var text = Encoding.UTF8.GetString(result);

                // Expected response : MediaBrowserServer|192.168.1.1:1234
                // If the response is what we're expecting, proceed
                if (text.StartsWith("mediabrowserserver", StringComparison.OrdinalIgnoreCase))
                {
                    text = text.Split('|')[1];

                    var vals = text.Split(':');

                    return new IPEndPoint(IPAddress.Parse(vals[0]), int.Parse(vals[1]));
                }
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
