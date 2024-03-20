using System.Net;
using System.Net.Sockets;
using codecrafters_redis;
using Command = codecrafters_redis.Command;

public class Program
{
    public static void Main(string[] args)
    {
        var argNumber = 0;
        var port = 6379;
        var isMaster = true;
        var masterHost = "";
        var masterPort = 0;
        while (argNumber < args.Length)
        {
            if (args[argNumber] == "--port")
            {
                argNumber++;
                port = int.Parse(args[argNumber]);
                argNumber++;
            }
            else if (args[argNumber] == "--replicaof")
            {
                isMaster = false;
                argNumber++;
                masterHost = args[argNumber];
                argNumber++;
                masterPort = int.Parse(args[argNumber]);
                argNumber++;
            }
        }
        
        var serverInfo = new ServerInfo
        {
            Role = isMaster ? "master" : "slave",
            MasterReplOffset = 0,
            MasterReplid = CreateRandomString(40)
        };

        var server = new TcpListener(IPAddress.Any, port);
        server.Start();

        if (!isMaster)
        {
            // We need to ping the master server
            var client = new TcpClient(masterHost, masterPort);
            var stream = client.GetStream();
            var bytes = System.Text.Encoding.UTF8.GetBytes("*1\r\n$4\r\nping\r\n");
            stream.Write(bytes, 0, bytes.Length);
            
            var data = new Byte[256];
            var bytesRead = stream.Read(data, 0, data.Length);
            var responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);
           
            bytes = System.Text.Encoding.UTF8.GetBytes("*3\r\n$8\r\nREPLCONF\r\n$14\r\nlistening-port\r\n$4\r\n6380\r\n");
            stream.Write(bytes, 0, bytes.Length);
            
            bytesRead = stream.Read(data, 0, data.Length);
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);
            
            bytes = System.Text.Encoding.UTF8.GetBytes("*3\r\n$8\r\nREPLCONF\r\n$4\r\ncapa\r\n$6\r\npsync2\r\n");
            stream.Write(bytes, 0, bytes.Length);
            
            bytesRead = stream.Read(data, 0, data.Length);
            responseData = System.Text.Encoding.ASCII.GetString(data, 0, bytesRead);
            
            bytes = System.Text.Encoding.UTF8.GetBytes("*3\r\n$5\r\nPSYNC\r\n$1\r\n?\r\n$2\r\n-1\r\n");
            stream.Write(bytes, 0, bytes.Length);
        }
        
        var eventLoop = new EventLoop(serverInfo);
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

    private static string CreateRandomString(int length)
    {
        var rnd = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => (char)('a' + rnd.Next(0, 26))).ToArray());
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
                        "replconf" => new Replconf(),
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