using System.Net.Sockets;
using codecrafters_redis;

public class SocketConnection
{
    private readonly EventLoop _eventLoop;
    private readonly Socket _socket;
    private readonly bool _asReplica;

    public SocketConnection(EventLoop eventLoop, Socket socket, bool asReplica)
    {
        _eventLoop = eventLoop;
        _socket = socket;
        _asReplica = asReplica;
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
                    "psync" => new Psync { ReplicationId = segments[1], ReplicationOffset = int.Parse(segments[2])},
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
                    _eventLoop.AddCommand( new CommandParams
                    {
                        Command = command, 
                        Socket = _socket, 
                        AsReplica = _asReplica
                    });
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