using System.Net;
using System.Net.Sockets;

// You can use print statements as follows for debugging, they'll be visible when running tests.
Console.WriteLine("Logs from your program will appear here!");

// Uncomment this block to pass the first stage
var server = new TcpListener(IPAddress.Any, 6379);
server.Start();
var socket = server.AcceptSocket(); // wait for client

var buffer = new byte[1024];

while (true)
{
    socket.Receive(buffer);
    var bytes = System.Text.Encoding.UTF8.GetBytes("+PONG\r\n");
    socket.Send(bytes);
}


