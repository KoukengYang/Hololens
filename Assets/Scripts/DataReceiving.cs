//----------------------------------------------------------------------------
// File:        DataReceiving.cs
// Authors:     Original script by HolotoolKit from Microsoft
//              which was heavily modified by Koukeng Yang (Keng) and
//              Michael Tanaya
//
// DateCreated: July 2016
//
// Notes:       this script opens a stream socket listener on a sperate thread
//              and keeps the seperate thread alive until program exits.
//
//----------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using HoloToolkit.Unity;
using System;

#if !UNITY_EDITOR
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Networking;
using Windows.Foundation;
#endif

public class DataReceiving : Singleton<DataReceiving>
{
    //STATIC FLAGS FOR DENOTING WHAT PACKAGE TYPE THIS IS
    public static int MESH_FLAG = 1;
    public static int OBJECT_CREATE_FLAG = 2;
    public static int STRING_FLAG = 3;
    public static int OBJECT_MOVE_FLAG = 4;
    public static int CAMERA_FLAG = 5;
    public static int HOLO_HEAD_CREATION_FLAG = 6;
    public static int AVATAR_CREATION_FLAG = 7;
    public static int AVATAR_DELETION_FLAG = 8;

    //Temporarily hard coded ip/port here but
    //the adress and port input in the Unity Editor WILL OVERRIDE these values
    public string hololensIPAddress = "192.168.0.3";
    public string hololensPortNumber = "46000";

    /// Temporary buffer for the data we are sending.
    private byte[] nextDataBufferToReceive;

    /// A queue of data buffers to send.
    public Queue<byte[]> dataQueueMesh = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS
    public Queue<byte[]> dataQueueObject = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS
    public Queue<byte[]> dataQueueString = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS
    public Queue<byte[]> dataQueueMoveObject = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS
    public Queue<byte[]> dataQueueAvatarCreateObject = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS
    public Queue<byte[]> dataQueueAvatarDelete = new Queue<byte[]>(); //MAKE THESE PRIVATE AND ADD GETTER FUNCTIONS

    //locks for the queue objects
    public static object queueMeshLock = new object();
    public static object queueObjectCreationLock = new object();
    public static object queueStringLock = new object();
    public static object queueMoveObjectLock = new object();
    public static object queueAvatarCreateLock = new object();
    public static object queueAvatarDeleteLock = new object();

#if !UNITY_EDITOR
        /// Tracks the network connection to the remote machine we are sending meshes to.
        private StreamSocketListener networkConnection;

       
        //********************************************************************
        //Unity Start call
        //********************************************************************
        public void Start()
        {
                //Debug.Log("Calling recieve function");
                ReceiveDataOverNetwork();
        }


        //********************************************************************
        // Recieve the data over the network.
        //********************************************************************
        private async void ReceiveDataOverNetwork()
        {

            // Setup a connection to the server.
            //host name for the IP connection 
            HostName networkHost = new HostName(hololensIPAddress.Trim());
            networkConnection = new StreamSocketListener(); 

            // Connections are asynchronous.  
            // NOTE These do not arrive on the main Unity Thread. Most Unity operations will throw in the callback 
            //bind it to the function to invoke on connection
            networkConnection.ConnectionReceived += NetworkConnectedHandler;
          
           try
           {
            //bind the listener to port and IP
            await networkConnection.BindEndpointAsync(networkHost, hololensPortNumber.ToString());
           }
           catch (Exception e)
           {
                Debug.Log(e.ToString());
           }      
        }

        //********************************************************************
        // Called when a connection attempt complete, successfully or not.  
        // NOTE These do not arrive on the main Unity Thread. Most Unity operations will throw in the callback
        //
        // sender --   Stream Socket info
        // args    -- Data about the async operation
        //********************************************************************
        public async void NetworkConnectedHandler(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            //Debug.Log("Remote HostName = " + args.Socket.Information.RemoteHostName);        
            //Debug.Log("Remote port = " + args.Socket.Information.RemotePort);
            // Status completed is successful.
            if (sender != null)
            {
                
                //Debug.Log("PREPARING TO READ DATA...");
                    
                DataReader networkDataReader;
                
                // Since we are connected, we can read the data coming in.
                using(networkDataReader = new DataReader(args.Socket.InputStream ))
                {      
                while (true)
                {       
                        try
                        { 
                            await networkDataReader.LoadAsync(sizeof(int));

                            //Debug.Log("Buffer inputlength = " + networkDataReader.UnconsumedBufferLength);

                            int tempPacketSize = networkDataReader.ReadInt32();
                            tempPacketSize = swapEndianInt(tempPacketSize);

                            //Debug.Log("first number: " + tempPacketSize);   

                            //Debug.Log("Buffer inputlength after reading 1st int = " + networkDataReader.UnconsumedBufferLength);       

                            //IF THIS IS < 0 THERE WILL BE ERRORS. THE PACKET SHOULD ACTUALLY NEVER BE < 0
                            uint actualPacketSize = (uint)(tempPacketSize - 4);  //minus 4 for the size of the packet included in the message

                            await networkDataReader.LoadAsync(actualPacketSize); 

                            //Debug.Log("Buffer inputlength after reading all data = " + networkDataReader.UnconsumedBufferLength);  
            
                            //******READ THE FLAG***************
                            int packetFlag = networkDataReader.ReadInt32();
                            packetFlag = swapEndianInt(packetFlag);
                            //Debug.Log("PACKET FLAG IS: " + packetFlag);


                            //******READ THE DATA***************
                            byte[] theActualData = new byte[(actualPacketSize - 4)]; //minus 4 for taking out the flag data
                            networkDataReader.ReadBytes(theActualData);                  

                            DataReceiveHandler(theActualData,packetFlag);
                        
                        }
                        catch (Exception e)
                        {
                            Debug.Log(e);
                        }
                    }
                }
            }
            else
            {
                //Debug.Log("Failed to establish connection." );
                networkConnection.Dispose();

            }
        }

        //********************************************************************
        // Called when Receiving data has completed.
        //
        //
        //Data -- The actual data.
        //flag -- the type of data coming in
        //********************************************************************
        public void DataReceiveHandler(byte[] Data, int flag)
        {
            // If we succeed, queue the data and set the deferral time.
            if (Data.Length > 0)
            {
                //Debug.Log("DATA Receive COMPLETED");

                if(flag == MESH_FLAG)
                {
                    lock(queueMeshLock)
                    {
                        dataQueueMesh.Enqueue(Data); 
                    }
                }
                else if(flag == OBJECT_CREATE_FLAG)
                {
                    lock(queueMeshLock)
                    { 
    
                        dataQueueObject.Enqueue(Data); 
                    }
                }
                else if(flag == STRING_FLAG)
                {
                    lock(queueStringLock)
                    { 
                            dataQueueString.Enqueue(Data); 
                    }
                }
                else if (flag == OBJECT_MOVE_FLAG)
                {
                    lock (queueMoveObjectLock)
                    {
                        dataQueueMoveObject.Enqueue(Data);
                    }
                }
                else if (flag == AVATAR_CREATION_FLAG)
                {
                    lock(queueAvatarCreateLock)
                    {
                        dataQueueAvatarCreateObject.Enqueue(Data);
                    }
                }
                else if (flag == AVATAR_DELETION_FLAG)
                {
                    lock(queueAvatarDeleteLock)
                    {
                        dataQueueAvatarDelete.Enqueue(Data);
                        //Debug.Log("dataQueueAvatarDelete size: " + dataQueueAvatarDelete.Count);
                    }
                }
                else
                {
                    Debug.Log("Unknown flag found. Flag value = " + flag);    
                }      
            }
            else
            {
                 Debug.Log("DATA Receive INCOMPLETE");
            }

        }
#endif


    //********************************************************************
    // Function grabBufferCommand will dequeue the package from the 
    // apropriate queue to execute
    //
    // flag -- the flag to tell which queue to pull from
    //********************************************************************
    public byte[] grabBufferCommand(int flag)
    {
        if (flag == MESH_FLAG)
        {
            lock (queueMeshLock)
            {
                return dataQueueMesh.Dequeue();
            }
        }
        else if (flag == OBJECT_CREATE_FLAG)
        {
            lock (queueObjectCreationLock)
            {
                return dataQueueObject.Dequeue();
            }
        }
        else if (flag == STRING_FLAG)
        {
            lock (queueStringLock)
            {
                return dataQueueString.Dequeue();
            }
        }
        else if (flag == OBJECT_MOVE_FLAG)
        {
            lock (queueMoveObjectLock)
            {
                return dataQueueMoveObject.Dequeue();
            }
        }
        else if (flag == AVATAR_CREATION_FLAG)
        {
            lock (queueAvatarCreateLock)
            {
                return dataQueueAvatarCreateObject.Dequeue();
            }
        }
        else if (flag == AVATAR_DELETION_FLAG)
        {
            lock (queueAvatarDeleteLock)
            {
                //Debug.Log("About to dequeue from avatar delete queue, size of queue BEFORE DEQUEUE: " + dataQueueAvatarDelete.Count);
                return dataQueueAvatarDelete.Dequeue();
            }
        }
        else
        {
            Debug.Log("UNKNOWN FLAG FOUND:" + flag);
            return null;
        }  
    }

    //********************************************************************
    // Utility Function to swap endian of a int
    //
    // numberToSwap -- the integer to swap endian, returns a new int with 
    //                 endian swapped.
    //********************************************************************
    public int swapEndianInt(int numberToSwap)
    {
        //convert the int to byte[]
        byte[] bytes = BitConverter.GetBytes(numberToSwap);

        byte t = bytes[0];
        bytes[0] = bytes[3];
        bytes[3] = t;

        t = bytes[1];
        bytes[1] = bytes[2];
        bytes[2] = t;

        // Then bitconverter can read the int32.
        return BitConverter.ToInt32(bytes, 0);
    }




}

