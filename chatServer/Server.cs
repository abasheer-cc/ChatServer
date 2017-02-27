//Name: Amshar Basheer
//Project Name: chatServer
//File Name: Server.cs
//Date: 11/3/2014
//Description: This chat server works by listening for client requests, and starting a new thread to deal with each client before returning to listening.
//  The client handling thread will receive messages from its client and echo them to all connected clients.  The client handling thread will continue until
//  the quit message is received, and then it will close the socket for its client.
//Note: the synchronous server socket example on MSDN was used as a starting point: http://msdn.microsoft.com/en-us/library/w89fhyex%28v=vs.110%29.aspx
//Feb 2017 modification: now also supports encryption using AesManaged class from System.Security.Cryptography.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Security.Cryptography;
using System.IO;

namespace chatServer
{
    public class Server
    {
        //list used to keep track of connected clients so can send messages to all of them
        private static List<Socket> socketList = new List<Socket>();

        // Incoming data from the client.
        public static string data = null;

        // Incoming decrypted message from the client.
        public static string decryptData = null;

        //AES Key and IV (initialization vector) length constants
        private const int kKeyLength = 32;
        private const int kIvLength = 16;


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
        //  Feb 2017 modification: now also supports encryption using AesManaged class from System.Security.Cryptography.
        public static void ClientHandler(object info)
        {
            // Data buffer for incoming data.
            byte[] bytes = new Byte[1024];
            data = null;
            decryptData = null;
            bool isEncrypted = false;
            bool isDataQuit = false;
            bool isDecryptDataQuit = false;

            //extract Socket type parameter from object info
            Socket handler = (Socket)info;
            
            try
            {
                do
                {
                    isDataQuit = false;
                    isDecryptDataQuit = false;

                    // An incoming connection needs to be processed.
                    while (true)
                    {
                        bytes = new byte[1024];
                        int bytesRec = handler.Receive(bytes);
                        data = Encoding.ASCII.GetString(bytes, 0, bytesRec);

                        //if read string as is and find <EOF> that means unencrypted, so can break out of infinite loop
                        if (data.IndexOf("<EOF>") > -1)
                        {
                            isEncrypted = false;
                            break;
                        }

                        //if reached this point in loop then encrypted 
                        //start by making byte array of size of bytes received then copy into it the filled bytes of the 1024
                        byte[] filledBytes = new byte[bytesRec];
                        Array.Copy(bytes, 0, filledBytes, 0, bytesRec);

                        //make byte arrays for key, iv, and encrypted message
                        byte[] key = new byte[kKeyLength];
                        byte[] iv = new byte[kIvLength];
                        byte[] encryptedMsg = new byte[filledBytes.Length - kKeyLength - kIvLength];

                        //bytes received contains key in first kKeyLength bytes, then iv in next kIvLength, then rest is the encryped message
                        //copy respective portions into their byte arrays
                        Array.Copy(filledBytes, 0, key, 0, kKeyLength);
                        Array.Copy(filledBytes, kKeyLength, iv, 0, kIvLength);
                        Array.Copy(filledBytes, kKeyLength + kIvLength, encryptedMsg, 0, filledBytes.Length - kKeyLength - kIvLength);

                        //Decrypt the encrypted message bytes to a string.
                        decryptData = DecryptStringFromBytes_Aes(encryptedMsg, key, iv);

                        //if read decrypted message string and find <EOF> that means encrypted, so can break out of infinite loop
                        if (decryptData.IndexOf("<EOF>") > -1)
                        {
                            isEncrypted = true;
                            break;
                        }
                    }

                    // Show the data on the console 
                    Console.WriteLine("Text received : {0}", data);
                    //if was encrypted then also show decrypted data
                    if (isEncrypted)
                    {
                        Console.WriteLine("Decrypted text received : {0}", decryptData);
                    }

                    if (!isEncrypted) //unencrypted
                    {
                        int colonIndex = data.IndexOf(":");
                        int eofIndex = data.IndexOf("<EOF>");
                        string dataSubstring = data.Substring(colonIndex + 2, eofIndex - (colonIndex + 2)); //+2 b/c msg starts 2 spots past colon (colon space then msg)
                        byte[] msg;

                        if (dataSubstring != "quit") //if not quit message
                        {
                            // Echo the data back to the clients.
                            msg = Encoding.ASCII.GetBytes(data);                            
                        }
                        else //quit message
                        {
                            isDataQuit = true;
                            string userName = data.Substring(0, colonIndex);
                            // Echo the modified quit message data back to the clients.
                            msg = Encoding.ASCII.GetBytes(userName + " has left<EOF>");
                        }
                        
                        //iterate through socketList and send msg through each so each client gets it
                        foreach (Socket s in socketList)
                        {
                            s.Send(msg);
                        }
                    }
                    else //encrypted
                    {
                        int colonIndex = decryptData.IndexOf(":");
                        int eofIndex = decryptData.IndexOf("<EOF>");
                        string decryptDataSubstring = decryptData.Substring(colonIndex + 2, eofIndex - (colonIndex + 2)); //+2 b/c msg starts 2 spots past colon (colon space then msg)

                        if (decryptDataSubstring != "quit") //if not quit message
                        {
                            //iterate through socketList and send same received msg through each so each client gets it
                            foreach (Socket s in socketList)
                            {
                                s.Send(bytes);
                            }
                        }
                        else //quit message
                        {
                            isDecryptDataQuit = true;
                            byte[] encryptedMsgPlusAesData;                            
                            string userName = decryptData.Substring(0, colonIndex);

                            // Echo the modified quit message data back to the clients.

                            // Create a new instance of the AesManaged
                            // class.  This generates a new key and initialization 
                            // vector (IV).
                            using (AesManaged myAes = new AesManaged())
                            {
                                byte[] encryptedMsg;
                                // Encrypt the modified quit message string to an array of bytes.
                                encryptedMsg = EncryptStringToBytes_Aes(userName + " has left<EOF>", myAes.Key, myAes.IV);

                                //make a new byte array that contains the key, iv, and encrypted message
                                encryptedMsgPlusAesData = new byte[kKeyLength + kIvLength + encryptedMsg.Length];
                                Array.Copy(myAes.Key, 0, encryptedMsgPlusAesData, 0, kKeyLength);
                                Array.Copy(myAes.IV, 0, encryptedMsgPlusAesData, kKeyLength, kIvLength);
                                Array.Copy(encryptedMsg, 0, encryptedMsgPlusAesData, kKeyLength + kIvLength, encryptedMsg.Length);
                            }

                            //iterate through socketList and send msg through each so each client gets it
                            foreach (Socket s in socketList)
                            {
                                s.Send(encryptedMsgPlusAesData);
                            }
                        }
                    }
                } while (!isDataQuit && !isDecryptDataQuit);
                
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

        //this method encrypts a plainText string to an array of bytes using AES encryption Key and IV
        //This method was borrowed as is from: https://msdn.microsoft.com/en-us/library/system.security.cryptography.aesmanaged.aspx
        static byte[] EncryptStringToBytes_Aes(string plainText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (plainText == null || plainText.Length <= 0)
                throw new ArgumentNullException("plainText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");
            byte[] encrypted;
            // Create an AesManaged object
            // with the specified key and IV.
            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for encryption.
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {

                            //Write all data to the stream.
                            swEncrypt.Write(plainText);
                        }
                        encrypted = msEncrypt.ToArray();
                    }
                }
            }


            // Return the encrypted bytes from the memory stream.
            return encrypted;

        }

        //this method decrypts an encrypted array of bytes to a plainText string using AES encryption Key and IV
        //this method was borrowed as is from: https://msdn.microsoft.com/en-us/library/system.security.cryptography.aesmanaged.aspx
        static string DecryptStringFromBytes_Aes(byte[] cipherText, byte[] Key, byte[] IV)
        {
            // Check arguments.
            if (cipherText == null || cipherText.Length <= 0)
                throw new ArgumentNullException("cipherText");
            if (Key == null || Key.Length <= 0)
                throw new ArgumentNullException("Key");
            if (IV == null || IV.Length <= 0)
                throw new ArgumentNullException("IV");

            // Declare the string used to hold
            // the decrypted text.
            string plaintext = null;

            // Create an AesManaged object
            // with the specified key and IV.
            using (AesManaged aesAlg = new AesManaged())
            {
                aesAlg.Key = Key;
                aesAlg.IV = IV;

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                // Create the streams used for decryption.
                using (MemoryStream msDecrypt = new MemoryStream(cipherText))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {

                            // Read the decrypted bytes from the decrypting stream
                            // and place them in a string.
                            plaintext = srDecrypt.ReadToEnd();
                        }
                    }
                }

            }
            return plaintext;
        }
    }
}

