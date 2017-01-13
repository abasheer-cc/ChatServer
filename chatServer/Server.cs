//Name: Amshar Basheer
//Project Name: chatServer
//File Name: Server.cs
//Date: 11/3/2014
//Description: This chat server works by listening for client requests, and starting a new thread to deal with each client before returning to listening.
//  The client handling thread will receive messages from its client and echo them to all connected clients.  The client handling thread will continue until
//  the quit message is received, and then it will close the socket for its client.
//Note: the synchronous server socket example on MSDN was used as a starting point: http://msdn.microsoft.com/en-us/library/w89fhyex%28v=vs.110%29.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;


namespace chatServer
{
    public class Server
    {
        //list used to keep track of connected clients so can send messages to all of them
        private static List<Socket> socketList = new List<Socket>();

        // Incoming data from the client.
        public static string data = null;

        
        //Method Name: StartListening
        //Parameters: none
        //Return: void
        //Description: establishes socket, then listens for client connections, and if client connection received then starts client handling thread and adds
        //  socket to socket list before returning to listening.
        public static void StartListening()
        {
            // Establish the local endpoint for the socket.
            // borrowed this next block of code from stackoverflow http://stackoverflow.com/questions/1069103/how-to-get-my-own-ip-address-in-c             
            IPHostEntry host;
            IPAddress localIP = null;
            host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    localIP = ip;
                }
            }


            IPEndPoint localEndPoint = new IPEndPoint(localIP, 11000);

            // Create a TCP/IP socket.
            Socket listener = new Socket(AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and 
            // listen for incoming connections.
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(10);

                // Start listening for connections.
                while (true)
                {
                    Console.WriteLine("Waiting for a connection...");

                    // Program is suspended while waiting for an incoming connection.
                    Socket handler = listener.Accept();

                    //once receive a connection then make new parameterized thread that runs ClientHandler
                    //  and takes socket as parameter
                    ParameterizedThreadStart clientHandler = new ParameterizedThreadStart(ClientHandler);
                    Thread t = new Thread(clientHandler);

                    //add socket to socketList
                    socketList.Add(handler);

                    //start thread and pass it the socket as a argument
                    t.Start(handler);

                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();

        }


        //Method Name: Client Handler
        //Parameters: object info: used to receive socket
        //Return: void
        //Description: receives messages from client and echoes them out to all clients until quit message received. When quit message received then sends message
        //  to all clients informing that the given client has left, and then removes socket from list and closes the socket.
        public static void ClientHandler(object info)
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];
            data = null;
            //extract Socket type parameter from object info
            Socket handler = (Socket)info;
            
            try
            {
                do
                {
                    // An incoming connection needs to be processed.
                    while (true)
                    {
                        bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        data = Encoding.ASCII.GetString(bytes, 0, bytesRec);
                        if (data.IndexOf("<EOF>") > -1)
                        {
                            break;
                        }
                    }

                    // Show the data on the console.
                    Console.WriteLine("Text received : {0}", data);

                    if (!data.EndsWith("quit<EOF>"))
                    {
                        // Echo the data back to the clients.
                        byte[] msg = Encoding.ASCII.GetBytes(data);

                        //iterate through socketList and send msg through each so each client gets it
                        foreach (Socket s in socketList)
                        {
                            s.Send(msg);
                        }
                    }
                    else
                    {
                        int colonIndex = data.IndexOf(":");
                        string userName = data.Substring(0, colonIndex);
                        // Echo the data back to the clients.
                        byte[] msg = Encoding.ASCII.GetBytes(userName + " has left<EOF>");

                        //iterate through socketList and send msg through each so each client gets it
                        foreach (Socket s in socketList)
                        {
                            s.Send(msg);
                        }
                    }
                } while (!data.EndsWith("quit<EOF>"));
                
                //once quit command received then remove socket from socketList and close socket
                socketList.Remove(handler);
                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

        }

        public static int Main(String[] args)
        {
            StartListening();
            return 0;
        }
    }
}

