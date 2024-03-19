using System.Net;
using System.Net.Sockets;
using codecrafters_redis;

public class Program
{
    public static void Main(string[] args)
    {
        var port = 6379;
        if (args.Length > 1 && args[0] == "--port")
            port = int.Parse(args[1]);
        
        var server = new TcpListener(IPAddress.Any, port);
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
                    var segments = ParseValue(ref parser);

                    var type = segments[0].ToLowerInvariant();

                    Command? command = type switch
                    {
                        "ping" => new Ping(),
                        "echo" => new Echo { Message = segments[1] },
                        "info" => new Info { Section = segments[1] },
                        "get" => new Get { Key = segments[1] },
                        "set" => segments.Length > 4 && segments[3].ToLowerInvariant() == "px"
                            ? new Set { Key = segments[1], Value = segments[2], Px = int.Parse(segments[4]) }
                            : new Set { Key = segments[1], Value = segments[2] },
                        _ => null
                    };

                    Console.WriteLine(command);

                    if (command is not null)
                        _eventLoop.AddCommand(_socket, command);
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
            if (type == ':')
                return new[] { ParseInteger(ref parser) };
            return Array.Empty<string>();
        }

        private string ParseInteger(ref LrParser parser)
        {
            var sign = 1;
            if (parser.TryMatch('+')) sign = 1;
            else if (parser.TryMatch('-')) sign = -1;
            var number = parser.EatNumber();
            parser.Match("\r\n");
            return (sign * number).ToString();
        }

        private string ParseBulkString(ref LrParser parser)
        {
            var length = parser.EatNumber();
            parser.Match("\r\n");
            var text = parser.EatString(length);
            parser.Match("\r\n");
            return text;
        }

        private string[] ParseArray(ref LrParser parser)
        {
            var numberOfElements = parser.EatNumber();
            parser.Match("\r\n");

            var result = new string[numberOfElements];
            for (var i = 0; i < numberOfElements; i++)
            {
                result[i] = ParseValue(ref parser)[0];
            }

            return result;
        }
    }
}