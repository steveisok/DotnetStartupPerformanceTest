// Pick a free local port
using System.Net;
using System.Net.Sockets;
using System.Text;

int port = 7878;
var localEndPoint = new IPEndPoint(IPAddress.Loopback, port);

// Create a listening socket (server)
using Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
listener.Bind(localEndPoint);
listener.Listen(1);

// Start accepting asynchronously
var acceptTask = listener.AcceptAsync();

// Create a client socket and connect to the server
using Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
client.Connect(localEndPoint);

// Wait for the server to accept the connection
Socket server = acceptTask.Result;

// Client sends "hello world"
byte[] message = Encoding.UTF8.GetBytes("hello world");
client.Send(message);

// Server reads the data
byte[] buffer = new byte[1024];
int bytesRead = server.Receive(buffer);
string received = Encoding.UTF8.GetString(buffer, 0, bytesRead);
Console.WriteLine($"Server received: {received}");

// Shutdown sockets
client.Shutdown(SocketShutdown.Both);
server.Shutdown(SocketShutdown.Both);
client.Close();
server.Close();
listener.Close();