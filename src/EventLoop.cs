using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public class EventLoop
{
    private readonly ConcurrentQueue<(Socket, string[])> _commandQueue = new();
    
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
                
                if (command[0] == "ping")
                {
                    Console.WriteLine("Pong " + socket.Handle );
                    var bytes = System.Text.Encoding.UTF8.GetBytes("+PONG\r\n");
                    socket.Send(bytes);                    
                }
                else if (command[0] == "echo")
                {
                    Console.WriteLine("Echo " + socket.Handle );
                    var bytes = System.Text.Encoding.UTF8.GetBytes($"${command[1].Length}\r\n{command[1]}\r\n");
                    socket.Send(bytes);                    
                }

            }   
            
            Thread.Sleep(100);
        }
    }
}