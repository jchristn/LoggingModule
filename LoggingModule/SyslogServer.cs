﻿using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SyslogLogging
{
    /// <summary>
    /// Syslog server.
    /// </summary>
    public class SyslogServer
    {
        #region Public-Members

        /// <summary>
        /// Hostname.
        /// </summary>
        public string Hostname
        {
            get
            {
                return _Hostname;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(Hostname));
                _Hostname = value;
            }
        }

        /// <summary>
        /// UDP port.
        /// </summary>
        public int Port
        {
            get
            {
                return _Port;
            }
            set
            {
                if (value < 0) throw new ArgumentException("Port must be zero or greater.");
                _Port = value;
            }
        }

        #endregion

        #region Private-Members

        internal readonly object SendLock = new object();
        internal UdpClient Udp = null;
        private string _Hostname = "127.0.0.1";
        private int _Port = 514;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the object.
        /// </summary>
        public SyslogServer()
        {
            Udp = new UdpClient(Hostname, Port);
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
            Udp = new UdpClient(Hostname, Port);
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
