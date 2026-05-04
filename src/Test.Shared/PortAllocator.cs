namespace SyslogLogging.Tests.Shared
{
    using System.Net;
    using System.Net.Sockets;

    internal static class PortAllocator
    {
        public static int GetEphemeralPort()
        {
            using UdpClient client = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
            IPEndPoint endpoint = (IPEndPoint)client.Client.LocalEndPoint!;
            return endpoint.Port;
        }
    }
}
