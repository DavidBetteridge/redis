using System.Net;
using System.Net.Sockets;
using codecrafters_redis;

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

        var eventLoop = new EventLoop(serverInfo);
        var eventLoopThread = new Thread(eventLoop.ProcessCommands);
        eventLoopThread.Start();
        
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
            
            // var masterSocketConnection = new SocketConnection(eventLoop, client.Client, asReplica: true);
            // var masterSocketConnectionThread = new Thread(masterSocketConnection.Listen);
            // masterSocketConnectionThread.Start();
        }

        while (true)
        {
            var socket = server.AcceptSocket(); // wait for client
            Console.WriteLine("Opened socket connection " + socket.Handle);
            var socketConnection = new SocketConnection(eventLoop, socket, asReplica: false);
            var socketConnectionThread = new Thread(socketConnection.Listen);
            socketConnectionThread.Start();
        }
    }

    private static string CreateRandomString(int length)
    {
        var rnd = new Random();
        return new string(Enumerable.Range(0, length).Select(_ => (char)('a' + rnd.Next(0, 26))).ToArray());
    }
}