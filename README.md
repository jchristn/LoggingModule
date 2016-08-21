# bigq

[![][nuget-img]][nuget]

[nuget]:     https://www.nuget.org/packages/BigQ.dll
[nuget-img]: https://badge.fury.io/nu/Object.svg

messaging platform in C#

For a sample app exercising bigq, please see: https://github.com/bigqio/chat

## help or feedback
first things first - do you need help or have feedback?  Contact me at joel at maraudersoftware.com dot com or file an issue here!

## description
bigq is a messaging platform using TCP sockets and websockets (intentionally not using AMQP by design) featuring sync, async, channel, and private communications. bigq is written in C# and made available under the MIT license.  bigq is tested and compatible with Mono.

Core use cases for bigq:
- simple sockets wrapper - we make sockets programming easier
- standard communication layer connecting apps through diverse transports including:
  - TCP
  - TCP with SSL
  - Websockets
  - Websockets with SSL
- real-time messaging like chat applications
- flexible distribution options
  - unicast node to node
  - multicast channels for publisher-subscriber
  - broadcast channels
- cluster management
- near real-time notifications and events

## performance
bigq is still early in development.  While we have high aspirations on performance, it's not there yet.  The software has excellent stability in lower throughput environments with lower rates of network change (adds, removes).  Performance will be a focus area in the coming releases.

## components
Two main components to bigq: client and server.  The server can be run independently or instantiated within your own application.  Clients initiate connections to the server and maintain them to avoid issues with intermediary firewalls.  

## starting the server
Refer to the BigQServerTest project for a thorough example.
```
using BigQ;
...
// start the server
Server server = new Server(null);			// with a default configuration
Server server = new Server("server.json");	// with a configuration file

// set callbacks
server.MessageReceived = MessageReceived;		
server.ServerStopped = ServerStopped;				
server.ClientConnected = ClientConnected;
server.ClientLogin = ClientLogin;
server.ClientDisconnected = ClientDisconnected;
server.LogMessage = LogMessage;

// callback implementation, these methods should return true
static bool MessageReceived(Message msg) { ... }
static bool ClientConnected(Client client) { ... }
static bool ClientLogin(Client client) { ... }
static bool ClientDisconnected(Client client) { ... }
static bool LogMessage(string msg) { ... }
```

## starting the client
Refer to the BigQClientTest project for a thorough example.
```
using BigQ;

// start the client and connect to the server
Client client = new Client(null);			// with a default configuration
Client client = new Client("client.json");	// with a configuration file

// set callbacks
client.AsyncMessageReceived = AsyncMessageReceived;
client.SyncMessageReceived = SyncMessageReceived;
client.ServerConnected = ServerConnected;
client.ServerDisconnected = ServerDisconnected;
client.ClientJoinedServer = ClientJoinedServer;
client.ClientLeftServer = ClientLeftServer;
client.ClientJoinedChannel = ClientJoinedChannel;
client.ClientLeftChannel = ClientLeftChannel;
client.SubscriberJoinedChannel = SubscriberJoinedChannel;
client.SubscriberLeftChannel = SubscriberLeftChannel;
client.LogMessage = LogMessage;

// implement callbacks, these methods should return true
// sync message callback should return the data to be returned to requestor
static bool AsyncMessageReceived(Message msg) { ... }
static byte[] SyncMessageReceived(Message msg) { return Encoding.UTF8.GetBytes("Hello!"); }
static bool ServerConnected() { ... }
static bool ServerDisconnected() { ... }
static bool ClientJoinedServer(string clientGuid) { ... }
static bool ClientLeftServer(string clientGuid) { ... }
static bool ClientJoinedChannel(string clientGuid, string channelGuid) { ... }
static bool ClientLeftChannel(string clientGuid, string channelGuid) { ... }
static bool SubscriberJoinedChannel(string clientGuid, string channelGuid) { ... }
static bool SubscriberLeftChannel(string clientGuid, string channelGuid) { ... }
static bool LogMessage(string msg) { ... }

// login from the client
Message response;
if (!client.Login(out response)) { // handle failures }
```

## unicast messaging: one to one
unicast messages are sent directly between clients
```
Message response;
List<Client> clients;

// find a client to message
if (!client.ListClients(out response, out clients)) { // handle errors }

// private async message
// received by 'AsyncMessageReceived' on recipient
if (!client.SendPrivateMessageAsync(guid, msg)) { // handle errors }

// private sync message
// received by 'SyncMessageReceived' on recipient client
// which should return response data
if (!client.SendPrivateMessageSync(guid, "Hello!", out response)) { // handle errors }
```

## multicast messaging: one to many
messages sent to a multicast channel are sent to all subscribers
```
Message response;
List<Channel> channels;
List<Client> clients;

// publishers: list and join, or create a channel
if (!client.ListChannels(out response, out channels)) { // handle errors }
if (!client.JoinChannel(guid, out response)) { // handle errors }
if (!client.CreateChannel(guid, false, out response)) { // handle errors }

// subscribers subscribe to a channel
if (!client.SubscribeChannel(guid, out response)) { // handle errors }

// publishers send channel message to subscribers
// received by 'AsyncMessageReceived' on each client that is a member of that channel
if (!client.SendChannelMessage(guid, "Hello!")) { // handle errors }

// leave a channel, unsubscribe, or delete it if yours
if (!client.LeaveChannel(guid, out response)) { // handle errors }
if (!client.UnsubscribeChannel(guid, out response)) { // handle errors }
if (!client.DeleteChannel(guid, out response)) { // handle errors }

// list channel members or subscribers
if (!client.ListChannelMembers(guid, out response, out clients)) { // handle errors }
if (!client.ListChannelSubscribers(guid, out response, out clients)) { // handle errors }
```

## broadcast messaging: one to all
messages sent to a broadcast channel are sent to all members (not just subscribers)
```
Message response;
List<Channel> channels;
List<Client> clients;

// list and join, or create a channel
if (!client.ListChannels(out response, out channels)) { // handle errors }
if (!client.JoinChannel(guid, out response)) { // handle errors }
if (!client.CreateChannel(guid, false, out response)) { // handle errors }

// send channel message to all members
// received by 'AsyncMessageReceived' on each client that is a member of that channel
if (!client.SendChannelMessage(guid, "Hello!")) { // handle errors }

// leave a channel, unsubscribe, or delete it if yours
if (!client.LeaveChannel(guid, out response)) { // handle errors }
if (!client.DeleteChannel(guid, out response)) { // handle errors }

// list channel members
if (!client.ListChannelMembers(guid, out response, out clients)) { // handle errors }
```

## connecting using websockets
please refer to the sample Javascript chat application on github.

## connecting using SSL
when connecting using SSL, if you are using self-signed certificates, be sure to set 'AcceptInvalidSSLCerts' to true in the config file on both the server and client, and enable the TcpSSLServer (and configure it accordingly).  If this value is left to false, you will encounter exceptions if a node attempts to connect using a certificate that cannot be validated.  Be sure to use PFX files for your client and server certificates!

## authorization
bigq uses two filesystem files (defined in the server configuration file) to determine if messages should be authorized.  Please refer to the sample files in the project for their structure.  It is important to note that using this feature can and will affect performance.

## bigq framing
bigq uses a simple framing mechanism that closely follows HTTP.  A set of headers start each message, with each header ending in a carriage return and newline ```\r\n```.  The headers contain a variety of metadata, and most importantly, ContentLength, which indicates how many bytes are to be read after the header delimiter.  The header delimiter is an additional carriage return and newline ```\r\n``` which follows the carriage return and newline of the final header.  The body is internally treated as a byte array so the connected clients will need to manage encoding.
```
Email: foo@bar.com
ContentType: application/json
ContentLength: 22

{ first_name: 'joel' }
```

## sample server configuration file
multiple servers can be set to enabled at any given time.  Values for servers can NOT be changed while BigQ is running.  If you wish to start another server, or change a server's settings, BigQ will have to be restarted for those changes to take affect.
```
{  
   "Version":"1.0.0",
   "AcceptInvalidSSLCerts":true,
   "Files":{  
      "UsersFile":"users.json",
      "PermissionsFile":"permissions.json"
   },
   "Heartbeat":{  
      "Enable":false,
      "IntervalMs":1000,
      "MaxFailures":5
   },
   "Notification":{  
      "MsgAcknowledgement":false,
      "ServerJoinNotification":true,
      "ChannelJoinNotification":true
   },
   "Debug":{  
      "Enable":true,
      "LockMethodResponseTime":true,
      "MsgResponseTime":true,
      "ConsoleLogging":true
   },
   "TcpServer":{  
      "Enable":true,
      "IP":"0.0.0.0",
      "Port":8000
   },
   "TcpSSLServer":{  
      "Enable":true,
      "IP":"127.0.0.1",
      "Port":8001,
      "P12CertFile":"server.pfx",
      "P12CertPassword":"password"
   },
   "WebsocketServer":{  
      "Enable":false,
      "IP":"0.0.0.0",
      "Port":8002
   },
   "WebsocketSSLServer":{  
      "Enable":false,
      "IP":"0.0.0.0",
      "Port":8003,
      "P12CertFile":"server.pfx",
      "P12CertPassword":"password"
   }
}

```

## sample client configuration file
note: only one server can be set as enabled at a time!
```
{  
   "Version":"1.0.0",
   "GUID":"01234567-0123-0123-0123-012345678901",
   "Email":"",
   "Password":"",
   "AcceptInvalidSSLCerts":true,
   "SyncTimeoutMs":10000,
   "Heartbeat":{  
      "Enable":false,
      "IntervalMs":1000,
      "MaxFailures":5
   },
   "Debug":{  
      "Enable":true,
      "MsgResponseTime":true,
      "ConsoleLogging":true
   },
   "TcpServer":{  
      "Enable":true,
      "IP":"0.0.0.0",
      "Port":8000
   },
   "TcpSSLServer":{  
      "Enable":false,
      "IP":"0.0.0.0",
      "Port":8001,
      "P12CertFile":"client.pfx",
      "P12CertPassword":"password"
   }
}

```

## running under Mono
BigQ works well in Mono environments to the extent that we have tested it.  It is recommended that when running under Mono, you execute the containing EXE using --server and after using the Mono Ahead-of-Time Compiler (AOT).
```
mono --aot=nrgctx-trampolines=8096,nimt-trampolines=8096,ntrampolines=4048 --server myapp.exe
mono --server myapp.exe
```
