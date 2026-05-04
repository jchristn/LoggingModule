namespace SyslogLogging.Tests.Shared
{
    using System;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal sealed class UdpCaptureListener : IDisposable
    {
        private readonly UdpClient _Client;

        public int Port
        {
            get
            {
                IPEndPoint endpoint = (IPEndPoint)_Client.Client.LocalEndPoint!;
                return endpoint.Port;
            }
        }

        public UdpCaptureListener()
        {
            _Client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        }

        public async Task<string> ReceiveAsync(TimeSpan? timeout = null)
        {
            using CancellationTokenSource cts = new CancellationTokenSource(timeout ?? TimeSpan.FromSeconds(5));
            UdpReceiveResult result = await _Client.ReceiveAsync(cts.Token).ConfigureAwait(false);
            return Encoding.UTF8.GetString(result.Buffer);
        }

        public void Dispose()
        {
            _Client.Dispose();
        }
    }
}
