using System.Net;
using System.Net.Sockets;
using codecrafters_redis;


var server = new TcpListener(IPAddress.Any, 6379);
server.Start();

var eventLoop = new EventLoop();
var eventLoopThread = new Thread(eventLoop.ProcessCommands);
eventLoopThread.Start();

while (true)
{
    var socket = server.AcceptSocket(); // wait for client
    Console.WriteLine("Opened socket connection " + socket.Handle);
    var socketConnection = new SocketConnection(eventLoop, socket);
    var socketConnectionThread = new Thread(socketConnection.Listen);
    socketConnectionThread.Start();
}


class SocketConnection
{
    private readonly EventLoop _eventLoop;
    private readonly Socket _socket;

    public SocketConnection(EventLoop eventLoop, Socket socket)
    {
        _eventLoop = eventLoop;
        _socket = socket;
    }
    public void Listen()
    {
        var buffer = new byte[1024];
        while (true)
        {
            if (_socket.Receive(buffer) > 0)
            {
                
                
                _eventLoop.AddCommand(_socket);
                Console.WriteLine("Ping " + _socket.Handle);
            }
        }

    }  
}



//
//
// var backgroundThread = new Thread(new ThreadStart(Program.DoSomeHeavyLifting));
// // Start thread
// backgroundThread.Start();
//
// var connection = server.AcceptSocket(); // wait for client
// Thread(() => {
//     var buffer = new byte[1024];
//     while (true)
//     {
//         connection.Receive(buffer);
//         var bytes = System.Text.Encoding.UTF8.GetBytes("+PONG\r\n");
//         connection.Send(bytes);
//     }
// });
//
//
//
// var buffer = new byte[1024];
//
// while (true)
// {
//     connection.Receive(buffer);
//     var bytes = System.Text.Encoding.UTF8.GetBytes("+PONG\r\n");
//     connection.Send(bytes);
// }


