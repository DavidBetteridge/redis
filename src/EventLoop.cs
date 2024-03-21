using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public record CacheEntry
{
    public string Value { get; init; } = default!;
    public DateTime? ExpiresOn { get; init; }
}

public record CommandParams
{
    public Socket Socket { get; init; } = default!;
    public Command Command { get; init; } = default!;
    public bool AsReplica { get; init; }
}

public class EventLoop
{
    private readonly BlockingCollection<CommandParams> _commandQueue = new();
    
    private readonly Dictionary<string, CacheEntry> _cache = new ();
    private readonly ServerInfo _serverInfo;
    private readonly List<Socket> _replicas = new();

    public EventLoop(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
    }
    
    public void AddCommand(CommandParams commandParams)
    {
        _commandQueue.Add(commandParams);
    }
    
    public void ProcessCommands()
    {
        while (true)
        {
            if (_commandQueue.TryTake(out var commandParams))
            {
                var socket = commandParams.Socket;
                var command = commandParams.Command;
                var asReplica = commandParams.AsReplica;
                
                var response = command switch
                {
                    Ping => ProcessPing(),
                    Echo detail => ProcessEcho(detail),
                    Get detail => ProcessGet(detail),
                    Set detail => ProcessSet(detail, asReplica),
                    Info detail => ProcessInfo(detail),
                    Replconf detail => ProcessReplconf(detail),
                    Psync detail => ProcessPsync(detail, socket),
                    _ => throw new NotImplementedException()
                };
                if (response is not null)
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(response);
                    socket.Send(bytes);
                }
            }   
        }
    }

    private string? ProcessSet(Set set, bool asReplica)
    {
        _cache[set.Key] = new CacheEntry
        {
            Value = set.Value,
            ExpiresOn = set.Px.HasValue ? (DateTime.UtcNow + TimeSpan.FromMilliseconds(set.Px.Value)) : null
        };
        
        // Replica the writes
        foreach (var replica in _replicas)
        {
            var text = System.Text.Encoding.UTF8.GetString(set.Raw);
            Console.WriteLine("Sending " + text);
            replica.Send(set.Raw);
        }

        if (!asReplica)
            return SimpleString("OK");

        // Sets on replicas have no response
        return null;
    }

    private string ProcessGet(Get get)
    {
        if (_cache.TryGetValue(get.Key, out var cacheEntry))
        {
            if (!cacheEntry.ExpiresOn.HasValue || (cacheEntry.ExpiresOn.Value > DateTime.UtcNow))
            {
                return BulkString(cacheEntry.Value);
            }
        }

        return NullBulkString;
    }

    private string ProcessInfo(Info info)
    {
        var parts = new[]
        {
            $"master_replid:{_serverInfo.MasterReplid}",
            $"role:{_serverInfo.Role}",
            $"master_repl_offset:{_serverInfo.MasterReplOffset}",
        };

        var result = string.Join("\r\n", parts);
        return BulkString(result);
    }

    private string? ProcessPsync(Psync cmd, Socket socket)
    {
        var response = SimpleString($"FULLRESYNC {_serverInfo.MasterReplid} {_serverInfo.MasterReplOffset}");
        var bytes = System.Text.Encoding.UTF8.GetBytes(response);
        socket.Send(bytes);
        
        var content = Convert.FromBase64String(EmptyRDBFile);
        var preamble = System.Text.Encoding.UTF8.GetBytes($"${content.Length}\r\n");
        socket.Send(preamble.Concat(content).ToArray());
        
        _replicas.Add(socket);
        
        return null;
    }
    
    private string ProcessEcho(Echo echo) => BulkString(echo.Message);
    private string ProcessPing() => SimpleString("PONG");
    private string ProcessReplconf(Replconf replconf) => SimpleString("OK");
    
    private string SimpleString(string str) => $"+{str}\r\n";
    private string BulkString(string str) => $"${str.Length}\r\n{str}\r\n";
    
    private const string NullBulkString = "$-1\r\n";

    private const string EmptyRDBFile = "UkVESVMwMDEx+glyZWRpcy12ZXIFNy4yLjD6CnJlZGlzLWJpdHPAQPoFY3RpbWXCbQi8ZfoIdXNlZC1tZW3CsMQQAPoIYW9mLWJhc2XAAP/wbjv+wP9aog==";
}