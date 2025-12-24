namespace SyslogLogging
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;

    /// <summary>
    /// Syslog server with proper resource management and thread safety.
    /// </summary>
    public class SyslogServer : IDisposable
    {
        /// <summary>
        /// Hostname. Cannot be null or empty.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is null or empty.</exception>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (string.IsNullOrEmpty(value)) throw new ArgumentException("Hostname cannot be null or empty.", nameof(Hostname));
                _Hostname = value;

                SetUdp();
            }
        }

        /// <summary>
        /// UDP port. Valid range: 0-65535.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is not between 0 and 65535.</exception>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0 || value > 65535) throw new ArgumentOutOfRangeException(nameof(Port), "Port must be between 0 and 65535.");
                _Port = value;

                SetUdp();
            }
        }

        /// <summary>
        /// IP:port of the server.
        /// </summary>
        public string IpPort
        {
            get
            {
                return _Hostname + ":" + _Port;
            }
        }

        internal readonly object SendLock = new object();
        internal UdpClient? Udp = null;
        private string _Hostname = "127.0.0.1";
        private int _Port = 514;
        private bool _Disposed = false;

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SyslogServer()
        {
        }

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        /// <param name="hostname">Hostname.</param>
        /// <param name="port">Port.</param>
        public SyslogServer(string hostname = "127.0.0.1", int port = 514)
        {
            Hostname = hostname;
            Port = port;
        }

        /// <summary>
        /// Display a human-readable string of the object.
        /// </summary>
        /// <returns>String.</returns>
        public override string ToString()
        {
            return "Syslog server: " + Hostname + ":" + Port + " (ip:port " + IpPort + ")";
        }

        private void SetUdp()
        {
            ThrowIfDisposed();

            lock (SendLock)
            {
                try
                {
                    Udp?.Dispose();
                    Udp = null;
                    Udp = new UdpClient(_Hostname, _Port);
                }
                catch
                {
                    // If UdpClient creation fails, ensure we don't have a disposed reference
                    Udp = null;
                    throw;
                }
            }
        }

        private void ThrowIfDisposed()
        {
            if (_Disposed) throw new ObjectDisposedException(nameof(SyslogServer));
        }

        /// <summary>
        /// Dispose of the syslog server and release network resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Dispose of the syslog server.
        /// </summary>
        /// <param name="disposing">True if disposing managed resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_Disposed)
            {
                if (disposing)
                {
                    lock (SendLock)
                    {
                        Udp?.Dispose();
                        Udp = null;
                    }
                }
                _Disposed = true;
            }
        }

        /// <summary>
        /// Finalizer to ensure resources are cleaned up.
        /// </summary>
        ~SyslogServer()
        {
            Dispose(false);
        }
    }
}
