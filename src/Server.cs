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
                var text = System.Text.Encoding.UTF8.GetString(buffer);
                var parser = new LrParser(text);
                var command = ParseValue(ref parser);
                
                _eventLoop.AddCommand(_socket, command);
                Console.WriteLine(command[0] + " " + _socket.Handle);
            }
        }

    }

    private string[] ParseValue(ref LrParser parser)
    {
        var type = parser.EatChar();
        if (type == '*')
            return ParseArray(ref parser);
        if (type == '$')
            return new[] { ParseBulkString(ref parser) };
        return Array.Empty<string>();
    }

    private string ParseBulkString(ref LrParser parser)
    {
        var length = parser.EatNumber();
        parser.TryMatch("\r\n");
        var text = parser.EatString(length);
        parser.TryMatch("\r\n");
        return text;
    }

    private string[] ParseArray(ref LrParser parser)
    {
        var numberOfElements = parser.EatNumber();
        parser.TryMatch("\r\n");

        var result = new string[numberOfElements];
        for (var i = 0; i < numberOfElements; i++)
        {
            result[i] = ParseValue(ref parser)[0];
        }

        return result;
    }
}
