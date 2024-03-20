using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public record CacheEntry
{
    public string Value { get; init; } = default!;
    public DateTime? ExpiresOn { get; init; }
}
public class EventLoop
{
    private readonly BlockingCollection<(Socket, Command)> _commandQueue = new();
    
    private readonly Dictionary<string, CacheEntry> _cache = new ();
    private readonly ServerInfo _serverInfo;

    public EventLoop(ServerInfo serverInfo)
    {
        _serverInfo = serverInfo;
    }
    
    public void AddCommand(Socket socket, Command command)
    {
        _commandQueue.Add((socket, command));
    }
    
    public void ProcessCommands()
    {
        while (true)
        {
            if (_commandQueue.TryTake(out var socketAndCommand))
            {
                var socket = socketAndCommand.Item1;
                var command = socketAndCommand.Item2;
                
                var response = command switch
                {
                    Ping => ProcessPing(),
                    Echo echo => ProcessEcho(echo),
                    Get get => ProcessGet(get),
                    Set set => ProcessSet(set),
                    Info info => ProcessInfo(info),
                    Replconf replconf => ProcessReplconf(replconf),
                    _ => throw new NotImplementedException()
                };
                var bytes = System.Text.Encoding.UTF8.GetBytes(response);
                socket.Send(bytes);   
            }   
        }
    }

    private string ProcessSet(Set set)
    {
        _cache[set.Key] = new CacheEntry
        {
            Value = set.Value,
            ExpiresOn = set.Px.HasValue ? (DateTime.UtcNow + TimeSpan.FromMilliseconds(set.Px.Value)) : null
        };

        return SimpleString("OK");
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

    private string ProcessEcho(Echo echo) => BulkString(echo.Message);
    private string ProcessPing() => SimpleString("PONG");
    private string ProcessReplconf(Replconf replconf) => SimpleString("OK");
    
    private string SimpleString(string str) => $"+{str}\r\n";
    private string BulkString(string str) => $"${str.Length}\r\n{str}\r\n";
    
    private const string NullBulkString = "$-1\r\n";
}