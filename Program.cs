// See https://aka.ms/new-console-template for more information
using System;
using System.Net;
using System.Collections;
using System.Net.Sockets;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

// To execute C#, please define "static void Main" on a class
// named Solution.

class Solution
{

    static void Main(string[] args)
    {
        MulticastMessageServer();
    }

    private static void BroadcastServer()
    {

        // Define the broadcast address (255.255.255.255) as an IP address object
        IPAddress broadcastAddress = IPAddress.Broadcast;

        // Define the port number to use for the broadcast message
        int port = 12345;

        // Create a UDP socket and enable broadcast mode
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.EnableBroadcast = true;

        // Define the message to send
        string message = "Hello, world!";
        byte[] data = Encoding.UTF8.GetBytes(message);

        // Create an endpoint for the broadcast address and port
        IPEndPoint endpoint = new IPEndPoint(broadcastAddress, port);

        // Send the broadcast message
        socket.SendTo(data, endpoint);

        // Clean up the socket
        socket.Close();

        Console.WriteLine("Broadcast message sent.");
        Console.ReadKey();
    }

    private static void MulticastClient()
    {

        // create a multicast socket
        Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, 12345));

        // join the multicast group
        IPAddress multicastAddress = IPAddress.Parse("239.255.1.1");
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(multicastAddress, IPAddress.Any));

        // disable multicast loopback
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastLoopback, true);

        // receive data
        byte[] buffer = new byte[1024];
        EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        int bytesRead = socket.ReceiveFrom(buffer, ref remoteEndPoint);
        string message = Encoding.ASCII.GetString(buffer, 0, bytesRead);
        Console.WriteLine("Received message: " + message);

    }

    private static void TCPServerUsingSelect()
    {
        // create a listener socket
        Socket listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        IPEndPoint localEndPoint = new IPEndPoint(IPAddress.Any, 54321);
        listener.Bind(localEndPoint);
        listener.Listen(100);

        Console.WriteLine("Server started. Listening for connections...");

        // create a list to hold all connected clients
        List<Socket> clients = new List<Socket>();

        // create buffer to hold incoming data
        byte[] buffer = new byte[1024];

        // loop forever to accept incoming connections and handle data
        while (true)
        {
            // create a list of sockets to check for incoming data
            List<Socket> checkRead = new List<Socket>(clients);
            checkRead.Add(listener);

            // wait for any socket to be ready for reading, with a timeout of 1 second
            Socket.Select(checkRead, null, null, 1000);

            // check each socket for incoming data
            foreach (Socket socket in checkRead)
            {
                if (socket == listener)
                {
                    // if the listener socket is ready, accept the incoming connection
                    Socket client = listener.Accept();
                    clients.Add(client);
                    Console.WriteLine($"New client connected. Total clients: {clients.Count}");
                }
                else
                {
                    if (socket.Available == 0 || !socket.Connected)
                    {
                        // if the client has disconnected, remove the socket from the list of clients
                        clients.Remove(socket);
                        socket.Close();
                        Console.WriteLine($"Client disconnected. Total clients: {clients.Count}");
                        continue;
                    }
                    // if a client socket is ready, read the incoming data
                    int bytesRead = socket.Receive(buffer);

                    if (bytesRead == 0)
                    {
                        System.Console.WriteLine("0 bytesRead");
                    }
                    else
                    {
                        // broadcast the incoming data to all connected clients except the sender
                        foreach (Socket client in clients)
                        {
                            if (client != socket)
                            {
                                client.Send(buffer, bytesRead, SocketFlags.None);
                            }
                        }
                    }
                }
            }

            Thread.Sleep(3000);
            System.Console.WriteLine($"Rend current thread {Thread.CurrentThread.Name} for 3 secs");
        }
    }


    private static void MulticastMessageServer()
    {
        IPAddress multicastIP = IPAddress.Parse("239.255.1.1");
        int multicastPort = 12345;

        UdpClient udpClient = new UdpClient();
        udpClient.JoinMulticastGroup(multicastIP);
        udpClient.MulticastLoopback = true;

        while (true)
        {
            Console.Write("Enter a message: ");
            string message = Console.ReadLine();

            byte[] buffer = Encoding.ASCII.GetBytes(message);
            var cnt = udpClient.Send(buffer, buffer.Length, new IPEndPoint(multicastIP, multicastPort));

            Console.WriteLine($"Sent bytes: {cnt}");
        }
    }

    private static List<TcpClient> connectedClients = new List<TcpClient>();

    private static void LanchTcpServerInNonBlockingMode()
    {
        var listener = new TcpListener(IPAddress.Loopback, 11000);
        listener.Start();

        while (true)
        {
            // Wait for incoming data using the Select method
            Socket.Select(new List<Socket> { listener.Server }, null, null, 100);

            // Check if there is an incoming connection request
            if (listener.Pending())
            {
                System.Console.WriteLine("Accept client");
                // Accept the client connection
                TcpClient client = listener.AcceptTcpClient();

                // Add the client to the connectedClients list
                connectedClients.Add(client);
            }
            var disconnectedClients = new List<TcpClient>();
            // Loop through all connected clients and check if there is any incoming data
            foreach (TcpClient connectedClient in connectedClients)
            {
                try
                {
                    System.Console.WriteLine("BroadcastMessage");
                    Socket clientSocket = connectedClient.Client;

                    if (clientSocket.Poll(1000, SelectMode.SelectRead))
                    {
                        // Read the message from the client
                        byte[] messageBytes = new byte[4096];
                        int bytesRead = clientSocket.Receive(messageBytes, 0, 4096, SocketFlags.None);
                        string message = Encoding.ASCII.GetString(messageBytes, 0, bytesRead);

                        // Broadcast the message to all connected clients
                        BroadcastMessage(message);

                        // Close the client connection if the message is "quit"
                        if (message.ToLower() == "quit")
                        {
                            clientSocket.Shutdown(SocketShutdown.Both);
                            clientSocket.Close();
                            connectedClients.Remove(connectedClient);
                        }
                    }
                }
                catch (Exception)
                {
                    disconnectedClients.Add(connectedClient);
                }
            }

            foreach (var client in disconnectedClients)
            {
                connectedClients.Remove(client);
                client.Close();
            }

            Thread.Sleep(3000);
            System.Console.WriteLine("Rend thread 3 secs to do other thing");
        }
    }

    private static void BroadcastMessage(string message)
    {
        // Get the message as a byte array
        byte[] messageBytes = Encoding.ASCII.GetBytes(message);

        // Create a temporary file to hold the message
        string tempFilePath = Path.GetTempFileName();
        File.WriteAllBytes(tempFilePath, messageBytes);

        try
        {
            // Loop through all connected clients and send the message to each one
            foreach (TcpClient connectedClient in connectedClients)
            {
                Socket clientSocket = connectedClient.Client;
                clientSocket.SendFile(tempFilePath);
            }
        }
        finally
        {
            // Delete the temporary file
            File.Delete(tempFilePath);
        }
    }

}

