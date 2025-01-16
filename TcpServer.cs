using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

class TcpServer
{
    private const int defaultPort = 5000;
    private static int port = defaultPort;
    private static readonly ConcurrentBag<TcpClient> Clients = new ConcurrentBag<TcpClient>();

    public static async Task Main(string[] args)
    {
        if (args.Length > 0)
        {
            if( !int.TryParse(args[0], out port))
            {
                Console.WriteLine($"Usage: dotnet run -- portNumber (default {defaultPort})");
                return;
            }
        }

        TcpListener server = new TcpListener(IPAddress.Any, port);

        server.Start();
        Console.WriteLine($"Server started on port {port}. Waiting for clients...");

        while (true)
        {
            TcpClient client = await server.AcceptTcpClientAsync();
            Console.WriteLine("Client connected.");

            // Add the new client to the list of active clients
            Clients.Add(client);

            // Handle the client in a separate task
            _ = HandleClientAsync(client);
        }
    }

    private static async Task HandleClientAsync(TcpClient client)
    {
        try
        {
            using (NetworkStream stream = client.GetStream())
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) != 0)
                {
                    string receivedMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    Console.WriteLine($"Received: {receivedMessage}");

                    // Broadcast the message to all other clients
                    await BroadcastMessageAsync(client, receivedMessage);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Client error: {ex.Message}");
        }
        finally
        {
            Console.WriteLine("Client disconnected.");
            RemoveClient(client);
            client.Close();
        }
    }

    private static async Task BroadcastMessageAsync(TcpClient sender, string message)
    {
        byte[] messageBytes = Encoding.UTF8.GetBytes(message);

        foreach (var client in Clients)
        {
            if (client != sender && client.Connected)
            {
                try
                {
                    NetworkStream stream = client.GetStream();
                    await stream.WriteAsync(messageBytes, 0, messageBytes.Length);
                    Console.WriteLine($"sent to: {client.ToString()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error broadcasting to a client: {ex.Message}");
                }
            }
        }
    }

    private static void RemoveClient(TcpClient client)
    {
        Clients.TryTake(out _); // Remove the client from the list
    }
}
