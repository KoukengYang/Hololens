//----------------------------------------------------------------------------
// File:        DataSending.cs
// Authors:     Original script by HolotoolKit from Microsoft
//              which was heavily modified by Koukeng Yang (Keng)
//              and Michael Tanaya
//
// DateCreated: July 2016
//
// Notes:       This script is attached to the data sending object to send
//              packets of information to the other devices (kinect / VR)
//
//              The internal set up is complete. Outside classes only need
//              to call SendData(byte[] dataBufferToSend, int dataType)
//              function to send data to the other attached devices.
//
//  From the outside classes calling this one, outside classes should 
//  structure their packages like this to pass into 
//  SendData(byte[] dataBufferToSend, int dataType).
//  //********************************************************************
//  // Packet Structure
//  //      0       1       2       3        4       5        6 ---> n
//  //  ----------------------------------------------------------------
//  //  |       |       |       |       |        |       |     
//  //  |  flg  |  flg  |  flg  |  flg  |  data  |  data |  data---> etc     
//  //  |       |       |       |       |        |       | 
//  //  ----------------------------------------------------------------
//  //
//  //  first 4 bytes of byte array contain 4 bytes to denote an int 
//  //  describing what kind of package this is. The rest of the bytes
//  //  in the byte[] is the actual data
//  //
//  //********************************************************************
//
//  //********************************************************************
//  !!!!!!!!!!!!!!!!!!!*!*!*!*!EXTREMELY IMPORTANT!*!*!*!*!*!!!!!!!!!!!!!!
//  //********************************************************************
//
//  this internal DataSending class will concatenate the size of the message
//  onto the front of the package so...
//
//  DO NOT INCLUDE THE SIZE OF THE PACKET INTO THE PACKET ITSELF BEFORE 
//  PASSING IT TO SendData(byte[] dataBufferToSend, int dataType)
//
//  the full message AFTER IT'S BEEN THROUGH SendData() 
//  will look like this: 
//                      bytes 0-3 hold the size of full message -
//                               - full message including these first 4 bytes
//                      bytes 4-7 hold the flag to denote package type
//                      bytes 8+ contains the data
//----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using System.Text;
using HoloToolkit.Unity;
using System;

//these includes will work when compiling for the hololens but not within
//Unity, the Unity editor will complain and mark these as unknown reds
//as unity currently does not support UWP Windows programming APIs
#if !UNITY_EDITOR
using Windows.Networking.Sockets;
using Windows.Storage.Streams;
using Windows.Networking;
using Windows.Foundation;
#endif

public class DataSending : Singleton<DataSending>
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
        public string ServerIP = "192.168.0.2";
        public string ConnectionPort = "45000";
        
        // Tracks if we are currently sending a mesh.
        private bool Sending = false;

        /// Temporary buffer for the data we are sending.
        private byte[] nextDataBufferToSend;

        /// A queue of data buffers to send.
        private Queue<byte[]> dataQueue = new Queue<byte[]>();

    //this block of code in the (#if !UNITY_EDITOR) 
    //will compile and run in Hololens but Unity will complain
    //since Unity doesn't support UWP Windows APIs. 
#if !UNITY_EDITOR
        /// Tracks the network connection to the remote machine we are sending meshes to.
        private StreamSocket networkConnection;

        /// If we cannot connect to the server, we will wait before trying to reconnect.
        private float deferTime = 0.0f;
 
        /// If we cannot connect to the server, this is how long we will wait before retrying.
        private float timeToDeferFailedConnections = 10.0f;

        //********************************************************************
        //Unity Update call, will do once every frame
        //********************************************************************
        public void Update()
        {
            // Check to see if deferTime has been set.  
            // DeferUpdates will set the Sending flag to true for 
            // deferTime seconds.  
            if (deferTime > 0.0f)
            {
                DeferUpdates(deferTime);
                deferTime = 0.0f;
            }   

            // If we aren't sending data, but we have data to send, send it.
            if (!Sending && dataQueue.Count > 0)
            {
                //Debug.Log("Calling send function");
                byte[] nextPacket = dataQueue.Dequeue();

                //Debug.Log("Packetsize = " + nextPacket.Length);
                //Debug.Log("size of queue: " + dataQueue.Count.ToString());  

                SendDataOverNetwork(nextPacket);
            }
        }

        //********************************************************************
        // Handles waiting for some amount of time before trying to reconnect.
        //********************************************************************
        void DeferUpdates(float timeout)
        {
            Sending = true;
            Invoke("EnableUpdates", timeout);
        }

        /// Stops waiting to reconnect.
        void EnableUpdates()
        {
            Sending = false;
        }

        //********************************************************************
        // Sends the data over the network.
        // byte[] dataBufferToSend is The data buffer to send
        //********************************************************************
        private void SendDataOverNetwork(byte[] dataBufferToSend)
        {
            if (Sending)
            {
                // This shouldn't happen, but just in case.
                //Debug.Log("one at a time please");
                return;
            }

            // Track that we are sending a data buffer.
            Sending = true;

            // Set the next buffer to send when the connection is made.
            nextDataBufferToSend = dataBufferToSend;

            // Setup a connection to the server.
            HostName networkHost = new HostName(ServerIP.Trim());
            networkConnection = new StreamSocket();
            
            //Debug.Log("Connection step 1 has been done...");        

            // Connections are asynchronous.  
            // NOTE These do not arrive on the main Unity Thread. Most Unity operations will throw in the callback 
            IAsyncAction outstandingAction = networkConnection.ConnectAsync(networkHost, ConnectionPort.ToString());          

            AsyncActionCompletedHandler aach = new AsyncActionCompletedHandler(NetworkConnectedHandler);
            outstandingAction.Completed = aach;
            
        }

        //********************************************************************
        // Called when a connection attempt complete, successfully or not.  
        // NOTE These do not arrive on the main Unity Thread. Most Unity operations will throw in the callback
        //
        // asyncInfo -- Data about the async operation
        // status    -- the status of the operation
        //********************************************************************
        public void NetworkConnectedHandler(IAsyncAction asyncInfo, AsyncStatus status)
        {
            //Debug.Log("YOU CONNECTED TO: " + networkConnection.Information.RemoteAddress.ToString());

            // Status completed is successful.
            if (status == AsyncStatus.Completed)
            {
                //Debug.Log("PREPARING TO WRITE DATA...");
                    
                DataWriter networkDataWriter;
                
                // Since we are connected, we can send the data we set aside when establishing the connection.
                using(networkDataWriter = new DataWriter(networkConnection.OutputStream))
                {              
                    //Debug.Log("PREPARING TO WRITE size");
                    // Write how much data we are sending.
                    networkDataWriter.WriteInt32(nextDataBufferToSend.Length);

                    //Debug.Log("PREPARING TO WRITE DATA");
                    // Then write the data.
                    networkDataWriter.WriteBytes(nextDataBufferToSend);

                    // Again, this is an async operation, so we'll set a callback.
                    DataWriterStoreOperation dswo = networkDataWriter.StoreAsync();
                    dswo.Completed = new AsyncOperationCompletedHandler<uint>(DataSentHandler);
                }
            }
            else
            {
                //Debug.Log("Failed to establish connection. Error Code: " + asyncInfo.ErrorCode);
                // In the failure case we'll requeue the data and wait before trying again.
                networkConnection.Dispose();

                // Didn't send, so requeue the data.
                dataQueue.Enqueue(nextDataBufferToSend);

                // And set the defer time so the update loop can do the 'Unity things' 
                // on the main Unity thread.
                deferTime = timeToDeferFailedConnections;
            }
        }

        //********************************************************************
        // Called when sending data has completed.
        // !!! NOTE These do not arrive on the main Unity Thread. Most Unity operations will throw in the callback !!!
        //
        //operation -- The operation in flight.
        //status -- The status of the operation.
        //********************************************************************
        public void DataSentHandler(IAsyncOperation<uint> operation, AsyncStatus status)
        {
            // If we failed, requeue the data and set the deferral time.
            if (status == AsyncStatus.Error)
            {
                // didn't send, so requeue
                dataQueue.Enqueue(nextDataBufferToSend);
                deferTime = timeToDeferFailedConnections;
            }
            else
            {
                // If we succeeded, clear the sending flag so we can send another mesh
                //Debug.Log("DATA SENT COMPLETED");
                Sending = false;
            }

            // Always disconnect here since we will reconnect when sending the next mesh.  

            //Debug.Log("CLOSED THE CONNECTION");
            networkConnection.Dispose();
        }
#endif
    //********************************************************************
    // SendData public function to be accessible outside the class.
    // Queues up a data buffer to send over the network.
    //
    // dataBufferToSend -- The data buffer to send
    // dataType         -- the flag to denote what type of packet it is
    //                     (see static flags for more information)
    //
    //********************************************************************
    public void SendData(byte[] dataBufferToSend, int dataType)
    {
        //********************************************************************
        // Packet Structure
        //      0       1       2       3        4       5        6 ---> n
        //  ----------------------------------------------------------------
        //  |       |       |       |       |        |       |     
        //  |  flg  |  flg  |  flg  |  flg  |  data  |  data |  data ---etc>     
        //  |       |       |       |       |        |       | 
        //  ----------------------------------------------------------------
        //
        //  first 4 bytes of byte array contain 4 bytes to denote an int 
        //  describing what kind of package this is. The rest of the bytes
        //  in the byte[] is the actual data
        //
        //********************************************************************

        //create the full package
        byte[] fullpackage = new byte[dataBufferToSend.Length + (2 * sizeof(int))]; //add 8 bytes for the size of the package and the ID

        //convert the flag into a byte array to put into the head of the package
        byte[] flagData = BitConverter.GetBytes(dataType);

        //put flag data at front of package
        flagData.CopyTo(fullpackage, 0);

        //put the data into the full package
        for (int i = 0; i < dataBufferToSend.Length; i++)
        {
            fullpackage[(i + 4)] = dataBufferToSend[i]; //offset by 4 for the size of the int in the front
        }
        
        //Debug.Log("Size of package with flag & ID: " + fullpackage.Length);

        //enqueue the package to send
        dataQueue.Enqueue(fullpackage);
    }
}



