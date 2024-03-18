using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public class EventLoop
{
    private readonly ConcurrentQueue<(Socket, string[])> _commandQueue = new();
    
    private Dictionary<string, string> _cache = new ();
    
    public void AddCommand(Socket socket, string[] command)
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
                var commandType = command[0].ToLowerInvariant();
                if (commandType == "ping")
                {
                    Console.WriteLine("Pong " + socket.Handle );
                    var bytes = System.Text.Encoding.UTF8.GetBytes(SimpleString("PONG"));
                    socket.Send(bytes);                    
                }
                else if (commandType == "echo")
                {
                    Console.WriteLine("Echo " + socket.Handle );
                    var bytes = System.Text.Encoding.UTF8.GetBytes(BulkString(command[1]));
                    socket.Send(bytes);                    
                }

                else if (commandType == "set")
                {
                    Console.WriteLine($"set " + socket.Handle + ":: " + command[1] + " => " + command[2] );
                    _cache[command[1]] = command[2];
                    var bytes = System.Text.Encoding.UTF8.GetBytes(SimpleString("OK"));
                    socket.Send(bytes);                    
                }
                
                else if (commandType == "get")
                {
                    Console.WriteLine($"get " + socket.Handle + ":: " + command[1] );
                    if (_cache.TryGetValue(command[1], out var value))
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(BulkString(value));
                        socket.Send(bytes);      
                    }
                    else
                    {
                        var bytes = System.Text.Encoding.UTF8.GetBytes(NullBulkString);
                        socket.Send(bytes);
                    }
                    
                 
                }
                
            }   
            
            Thread.Sleep(100);
        }
    }

    private string SimpleString(string str) => $"+{str}\r\n";
    private string BulkString(string str) => $"${str.Length}\r\n{str}\r\n";
    private const string NullBulkString = "$-1\r\n";
}