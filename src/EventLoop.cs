using System.Collections.Concurrent;
using System.Net.Sockets;

namespace codecrafters_redis;

public class EventLoop
{
    private readonly ConcurrentQueue<Socket> _commandQueue = new();
    
    public void AddCommand(Socket socket)
    {
        _commandQueue.Enqueue(socket);
    }
    
    public void ProcessCommands()
    {
        while (true)
        {
            while (_commandQueue.TryDequeue(out var socket))
            {
                Console.WriteLine("Pong " + socket.Handle );
                var bytes = System.Text.Encoding.UTF8.GetBytes("+PONG\r\n");
                socket.Send(bytes);
            }   
            
            Thread.Sleep(100);
        }
    }
}