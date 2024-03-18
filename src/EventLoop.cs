using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public record CacheEntry
{
    public string Value { get; init; }
    public DateTime? ExpiresOn { get; init; }
}
public class EventLoop
{
    private readonly ConcurrentQueue<(Socket, Command)> _commandQueue = new();
    
    private readonly Dictionary<string, CacheEntry> _cache = new ();
    
    public void AddCommand(Socket socket, Command command)
    {
        _commandQueue.Enqueue((socket, command));
    }
    
    public void ProcessCommands()
    {
        while (true)
        {
            while (_commandQueue.TryDequeue(out var socketAndCommand))
            {
                var socket = socketAndCommand.Item1;
                var command = socketAndCommand.Item2;
                
                var response = command switch
                {
                    Ping => ProcessPing(),
                    Echo echo => ProcessEcho(echo),
                    Get get => ProcessGet(get),
                    Set set => ProcessSet(set),
                    _ => throw new NotImplementedException()
                };
                var bytes = System.Text.Encoding.UTF8.GetBytes(response);
                socket.Send(bytes);   
            }   
            
            Thread.Sleep(100);
        }
    }

    private string ProcessSet(Set set)
    {
        if (set.Px.HasValue)
            Console.WriteLine("Will Expires " + DateTime.UtcNow + TimeSpan.FromMilliseconds(set.Px.Value));
        else
            Console.WriteLine("Will not Expiry");
        
        _cache[set.Key] = new CacheEntry
        {
            Value = set.Value,
            ExpiresOn = set.Px.HasValue ? (DateTime.UtcNow + TimeSpan.FromMilliseconds(set.Px.Value+100)) : null
        };

        return SimpleString("OK");
    }

    private string ProcessGet(Get get)
    {
        if (_cache.TryGetValue(get.Key, out var cacheEntry))
        {
            if (cacheEntry.ExpiresOn.HasValue)
                Console.WriteLine("Expires " + cacheEntry.ExpiresOn.Value.ToString("O") + " Now is " + DateTime.UtcNow.ToString("O"));
            else
                Console.WriteLine("Does not Expiry");
            
            if (!cacheEntry.ExpiresOn.HasValue || (cacheEntry.ExpiresOn.Value > DateTime.UtcNow))
            {
                return BulkString(cacheEntry.Value);
            }
        }

        return NullBulkString;
    }

    private string ProcessEcho(Echo echo) => BulkString(echo.Message);
    private string ProcessPing() => SimpleString("PONG");
    
    private string SimpleString(string str) => $"+{str}\r\n";
    private string BulkString(string str) => $"${str.Length}\r\n{str}\r\n";
    private const string NullBulkString = "$-1\r\n";
}