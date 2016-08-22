﻿//----------------------------------------------------------------------------
// File:        netWorkManager.cs
// Authors:     Koukeng Yang (Keng), Michael Tanaya, Taran Christensen
// DateCreated: July 2016
//
// Note:        This script keeps track of data sending and data receiving
//              from the hololens to the other devices (kinect / VR) and vice
//              versa.
//----------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using HoloToolkit.Unity;
using System;

public class netWorkManager : MonoBehaviour
{
    //static list for the object to create
    public static int OBJECT_CUBE = 1;
    public static int OBJECT_SPHERE = 2;
    public static int OBJECT_TREE = 3;
    public static int OBJECT_AVATAR = 4;
    public Camera headPosition;

    //The data sending object to the server
    public DataSending dataSender;
    public DataReceiving dataReceiver;
    public TextMesh displayText;

    //the mapping manager that does the mapping of the room
    public SpatialMappingManager mapManager;
    public byte[] messageReceive;

    //test message to the server
    string message = "GREETINGS FROM THE HOLOLENS";

    public ObjectCreator listOfObjects;

    private bool canSendHololensPosition = false;

    private float MaxTimeTillNextHololensPositionUpdate = 1f / 10f; //default 15fps
    private float hololensPositionUpdateTimer = 2f;
    private int hololensPositionID;

    //private object objectLock = new object();

    // Use this for initialization
    void Start()
    {
        //**************************TESTING CONNECTION TO THE SERVER **************************
        //Send a greeting message to the other devices to let them know the hololens is
        //online and use this as a test to see if a connection could be made.
        byte[] messageData = Encoding.ASCII.GetBytes(message);
        dataSender.SendData(messageData, DataSending.STRING_FLAG);
        //**************************TESTING CONNECTION TO THE SERVER **************************
    }

    // Update is called once per frame
    void Update()
    {
        
        if (dataReceiver.dataQueueMesh.Count > 0)
        {
            //not done and don't need for the hololens
        }
        if (dataReceiver.dataQueueObject.Count > 0) //if there is an object in the object creation queue
        {
            messageReceive = dataReceiver.grabBufferCommand(DataReceiving.OBJECT_CREATE_FLAG);

            //call function to create the object
            this.createObject(messageReceive);
        }
        if (dataReceiver.dataQueueString.Count > 0) //if there is an object in the string queue
        {
            messageReceive = dataReceiver.grabBufferCommand(DataReceiving.STRING_FLAG);
            string something = Encoding.ASCII.GetString(messageReceive);

            //display it in hololens
            displayText.text = something;

            //Debug.Log("message receive:" + something);
        }
        if (dataReceiver.dataQueueMoveObject.Count > 0) //if there is an object in the object move queue
        {
            messageReceive = dataReceiver.grabBufferCommand(DataReceiving.OBJECT_MOVE_FLAG);

            //call function to move the object
            this.moveObject(messageReceive);
        }
        if (dataReceiver.dataQueueAvatarCreateObject.Count > 0) //if there is an object in the avatar create queue
        {
            messageReceive = dataReceiver.grabBufferCommand(DataReceiving.AVATAR_CREATION_FLAG);

            //call function to create it
            this.createAvatar(messageReceive);
        }
        if (dataReceiver.dataQueueAvatarDelete.Count > 0) //if there is an object in the avatar DELETE queue
        {
            messageReceive = dataReceiver.grabBufferCommand(DataReceiving.AVATAR_DELETION_FLAG);

            //call function to create it
            this.deleteAvatar(messageReceive);
        }



        if (canSendHololensPosition)
        {
            hololensPositionUpdateTimer -= Time.deltaTime;

            if (hololensPositionUpdateTimer < 0)
            {
                //send the data over 
                this.sendObjectMoveCoords(hololensPositionID.ToString());

                hololensPositionUpdateTimer = MaxTimeTillNextHololensPositionUpdate;
            }
        }



    }

    //------------------------------------------------------------------------
    // Function: sendMeshToUnity()
    //
    // Function sends the mesh generated by the spatialmapping manager 
    // to the other devices via an async TCP connection
    //
    //returns:  true  -- if it can sends meshes
    //          false -- it cannot send the meshes
    //------------------------------------------------------------------------
    public bool sendMeshToUnity()
    {
        try
        {
#if !UNITY_EDITOR
            List<MeshFilter> MeshFilters = mapManager.GetMeshFilters();

            Debug.Log("MeshFilter count " + MeshFilters.Count);

            for (int index = 0; index < MeshFilters.Count; index++)
            {
                List<Mesh> meshesToSend = new List<Mesh>();
                MeshFilter filter = MeshFilters[index];
                Mesh source = filter.sharedMesh;
                Mesh clone = new Mesh();
                List<Vector3> verts = new List<Vector3>();
                verts.AddRange(source.vertices);
            
                for(int vertIndex=0; vertIndex < verts.Count; vertIndex++)
                {
                    verts[vertIndex] = filter.transform.TransformPoint(verts[vertIndex]);
                }

                clone.SetVertices(verts); 
                clone.SetTriangles(source.triangles, 0);
                meshesToSend.Add(clone);
                byte[] serialized = SimpleMeshSerializer.Serialize(meshesToSend);

                Debug.Log("size of data (without the size included): " + serialized.Length);

                dataSender.SendData(serialized, DataSending.MESH_FLAG);
            }
#endif

            return true;
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.Log(e);
            return false;
        }
    }

    //------------------------------------------------------------------------
    // Function: sendObjectCoords(GameObject theObject, int objectType, int ID)
    //
    // Function sends the cordinates of the NEWLY created hologram object to
    // the other devices. (kinect / VR)
    //
    // theObject --     the game object to grab position cordinates from 
    // objectType --    what type of object this is. E.g. cube/sphere/tree
    // ID --            the ID of the object
    //------------------------------------------------------------------------
    public void sendObjectCoords(GameObject theObject, int objectType, int ID)
    {
        //Debug.Log("You got to the send object coord function");
#if !UNITY_EDITOR
        try
        {
            // Create a buffer
            List<byte> messageBuffer = new List<byte>();
                    
            //Debug.Log("adding positions");
            // Add our position
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.x));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.y));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.z));

            //Debug.Log("adding rotations");
            // Add our Rotation
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.x));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.y));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.z));
            
            //Debug.Log("adding object type, type of object: " + objectType);
            // Add our Object Type
            messageBuffer.AddRange(BitConverter.GetBytes(objectType));

            //Debug.Log("Adding ID");
            // Add our ID
            messageBuffer.AddRange(BitConverter.GetBytes(ID));

            //the byte[] to send
            byte[] theDataPackage = messageBuffer.ToArray();
            
            
            //Debug.Log("You're done setting up the package");

            dataSender.SendData(theDataPackage, DataSending.OBJECT_CREATE_FLAG);

        }
        catch(Exception e)
        {
           Debug.Log("Could not send the newly created cordinates to other devices. Error: " + e); 
        }

#endif
    }


    //------------------------------------------------------------------------
    // Function: sendObjectMoveCoords(string objectID)
    //
    // Function sends the cordinates of an EXISTING hologram object that was
    // moved to the other devices (kinect / VR)
    //
    // objectID -- the ID of the object that was recently moved
    //------------------------------------------------------------------------
    public void sendObjectMoveCoords(string objectID)
    {
        int ID = Int32.Parse(objectID);

        //go fetch the object with the desired ID
        GameObject theObject = ObjectCreator.objects[ID];

        if (theObject == null)
        {
            Debug.Log("Bad ID found when trying to send the new move cordinates for object. Object ID: " + objectID);
        }

#if !UNITY_EDITOR
        try
        {
            // Create a buffer
            List<byte> messageBuffer = new List<byte>();
                    
            //Debug.Log("adding positions");
            // Add our position
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.x));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.y));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.position.z));

            //Debug.Log("adding rotations");
            // Add our Rotation
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.x));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.y));
            messageBuffer.AddRange(BitConverter.GetBytes(theObject.transform.rotation.eulerAngles.z));
            
            //Debug.Log("Adding ID");
            // Add our ID
            messageBuffer.AddRange(BitConverter.GetBytes(ID));

            //the byte[] to send
            byte[] theDataPackage = messageBuffer.ToArray();
            
            
            //Debug.Log("You're done setting up the package");

            dataSender.SendData(theDataPackage, DataSending.OBJECT_MOVE_FLAG);

        }
        catch(Exception e)
        {
           Debug.Log("Could not send the new object move cordinates to other devices. Error: " + e); 
        }

#endif
    }

    //------------------------------------------------------------------------
    // Function: createAvatar(byte[] theData)
    //
    // Function creates an avatar for a kinect person based on the data coming in.
    //
    // theData -- the data packet to read information from
    //------------------------------------------------------------------------
    private void createAvatar(byte[] theData)
    {
        //NOTE: floats ARE NOT endian swapped, but ints are on the hololens.
        //      So you have to swap endian on every int read in.
        float posX = BitConverter.ToSingle(theData, 0);
        //posX = this.swapEndianFloat(posX);
        float posY = BitConverter.ToSingle(theData, 4);
        //posY = this.swapEndianFloat(posY);
        float posZ = BitConverter.ToSingle(theData, 8);
        //posZ = this.swapEndianFloat(posZ);

        float rotX = BitConverter.ToSingle(theData, 12);
        //rotX = this.swapEndianFloat(rotX);
        float rotY = BitConverter.ToSingle(theData, 16);
        //rotY = this.swapEndianFloat(rotY);
        float rotZ = BitConverter.ToSingle(theData, 20);
        //rotZ = this.swapEndianFloat(rotZ);

        int theID = BitConverter.ToInt32(theData, 24);
        dataReceiver.swapEndianInt(theID);

        GameObject avatar = Instantiate(Resources.Load("Avatar")) as GameObject;
        avatar.transform.position = new Vector3(posX, posY, posZ);

        //set the rotation
        Quaternion newRotation = Quaternion.Euler(posX, posY, posZ);
        avatar.transform.rotation = newRotation;

        avatar.name = theID.ToString();

        listOfObjects.addToListOfObjects(theID, avatar);

    }

    //------------------------------------------------------------------------
    // Function: deleteAvatar(byte[] theData)
    //
    // Function delete an avatar for a kinect person based on the data coming in.
    //
    // theData -- the data packet to read information from
    //------------------------------------------------------------------------
    private void deleteAvatar(byte[] theData)
    {
        //Debug.Log("You are inside the delete avatar code");

        //read the ID to go get which thing to remove
        int theID = BitConverter.ToInt32(theData, 0);
        dataReceiver.swapEndianInt(theID);

        Debug.Log("Avatar ID to delete: " + theID);

        //Debug.Log("You are deleting the packet with ID:" + theID);

        //grab it from the hash table
        GameObject holder;
        ObjectCreator.objects.TryGetValue(theID, out holder);

        if (holder != null)
        {
            //remove it from the hashtable
            ObjectCreator.objects.Remove(theID);

            //delete it from the scene
            Destroy(holder);
        }
    }

    //------------------------------------------------------------------------
    // Function: createObject(byte[] theData)
    //
    // Function creates an object based on the data coming in.
    //
    // theData -- the data packet to read information from
    //------------------------------------------------------------------------
    private void createObject(byte[] theData)
    {
        float posX = BitConverter.ToSingle(theData,0);
        //posX = this.swapEndianFloat(posX);
        float posY = BitConverter.ToSingle(theData, 4);
        //posY = this.swapEndianFloat(posY);
        float posZ = BitConverter.ToSingle(theData, 8);
        //posZ = this.swapEndianFloat(posZ);
        
        float rotX = BitConverter.ToSingle(theData, 12);
        //rotX = this.swapEndianFloat(rotX);
        float rotY = BitConverter.ToSingle(theData, 16);
        //rotY = this.swapEndianFloat(rotY);
        float rotZ = BitConverter.ToSingle(theData, 20);
        //rotZ = this.swapEndianFloat(rotZ);

        int theObject = BitConverter.ToInt32(theData, 24);
        dataReceiver.swapEndianInt(theObject);

        int theID = BitConverter.ToInt32(theData, 28);
        dataReceiver.swapEndianInt(theID);

        if (theObject == OBJECT_CUBE)
        {
            //Debug.Log("CREATE Posx = " + posX + " PosY = " + posY + " PosZ = " + posZ);

            GameObject cube = Instantiate(Resources.Load("Cube")) as GameObject;
            cube.transform.position = new Vector3(posX, posY, posZ);

            //set the rotation
            Quaternion newRotation = Quaternion.Euler(posX, posY, posZ);
            cube.transform.rotation = newRotation;

            cube.name = theID.ToString();

            listOfObjects.addToListOfObjects(theID, cube);
        }
        else if (theObject == OBJECT_SPHERE)
        {
            GameObject sphere = Instantiate(Resources.Load("Sphere")) as GameObject;
            sphere.transform.position = new Vector3(posX, posY, posZ);

            //set the rotation
            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            sphere.transform.rotation = newRotation;

            sphere.name = theID.ToString();

            listOfObjects.addToListOfObjects(theID, sphere);
        }
        else if (theObject == OBJECT_TREE)
        {
            GameObject tree = Instantiate(Resources.Load("Tree")) as GameObject;
            tree.transform.position = new Vector3(posX, posY, posZ);

            //set the rotation
            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            tree.transform.rotation = newRotation;

            tree.name = theID.ToString();

            listOfObjects.addToListOfObjects(theID, tree);
        }
        else
        {
            Debug.Log("UNKNOWN OBJECT TO SPAWN, OBJECT ID = " + theObject);
        }

    }

    //------------------------------------------------------------------------
    // Function: moveObject(byte[] theData)
    //
    // Function moves an object based on the data packet coming in.
    //
    // theData -- the data packet to read information from
    //------------------------------------------------------------------------
    private void moveObject(byte[] theData)
    {
        //Debug.Log("You got to the move object code");

        float posX = BitConverter.ToSingle(theData, 0);
        //posX = this.swapEndianFloat(posX);
        float posY = BitConverter.ToSingle(theData, 4);
        //posY = this.swapEndianFloat(posY);
        float posZ = BitConverter.ToSingle(theData, 8);
        //posZ = this.swapEndianFloat(posZ);

        float rotX = BitConverter.ToSingle(theData, 12);
        //rotX = this.swapEndianFloat(rotX);
        float rotY = BitConverter.ToSingle(theData, 16);
        //rotY = this.swapEndianFloat(rotY);
        float rotZ = BitConverter.ToSingle(theData, 20);
        //rotZ = this.swapEndianFloat(rotZ);

        int theID = BitConverter.ToInt32(theData, 24);
        dataReceiver.swapEndianInt(theID);

        //go fetch the ID
        GameObject theObjectToMove = ObjectCreator.objects[theID];

        if (theObjectToMove == null)
        {
            Debug.Log("the object retrieved was null");
        }
        else
        {
            //Debug.Log("the object was retrieved from teh hash table");
            //Debug.Log("MOVE Posx = " + posX + " PosY = " + posY + " PosZ" + posZ);
            //Debug.Log("MOVE Rotx = " + rotX + " RotY = " + rotY + " RotZ" + rotZ);

            //move object to correct position
            theObjectToMove.transform.position = new Vector3(posX, posY, posZ);

            //set the rotation
            Quaternion newRotation = Quaternion.Euler(rotX, rotY, rotZ);
            theObjectToMove.transform.rotation = newRotation;
        }
    }

    //------------------------------------------------------------------------
    // Function: sendCameraPosition(Vector3 CameraPosition)
    //
    // Function sends the cordinates to the kinect machine to 
    // set the display projector to.
    //
    // CameraPosition -- the camera position to set the display projector to.
    //------------------------------------------------------------------------
    public void sendCameraPosition(Vector3 CameraPosition, Quaternion CameraRotation, int IDofCamera)
    {
#if !UNITY_EDITOR
        try
        {
            // Create a buffer
            List<byte> messageBuffer = new List<byte>();
                    
            //Debug.Log("adding positions");
            // Add our position
            messageBuffer.AddRange(BitConverter.GetBytes(CameraPosition.x));
            messageBuffer.AddRange(BitConverter.GetBytes(CameraPosition.y));
            messageBuffer.AddRange(BitConverter.GetBytes(CameraPosition.z));
            
            //add rotation
            messageBuffer.AddRange(BitConverter.GetBytes(CameraRotation.eulerAngles.x));
            messageBuffer.AddRange(BitConverter.GetBytes(CameraRotation.eulerAngles.y));
            messageBuffer.AddRange(BitConverter.GetBytes(CameraRotation.eulerAngles.z));
            
            
            //the byte[] to send
            byte[] theDataPackage = messageBuffer.ToArray();
                  
            //Debug.Log("You're done setting up the package");

            dataSender.SendData(theDataPackage, DataSending.CAMERA_FLAG);

        }
        catch(Exception e)
        {
           Debug.Log("Could not send the new object move cordinates to other devices. Error: " + e); 
        }

#endif
    }

    //------------------------------------------------------------------------
    // Function: sendHoloHeadPosition()
    //
    // Function sends the cordinates to the kinect machine to 
    // set the display location of the hololens
    //
    //------------------------------------------------------------------------
    public void sendHoloHeadPosition()
    {
        listOfObjects.addHololensPositionToList(headPosition.transform.FindChild("CamPositionObject").gameObject);


    }

    //------------------------------------------------------------------------
    // Function: setHololensHeadPositionFlag(bool value)
    //
    // Function sets the hololens flag so that the program knows it is allowed
    // to send the hololens head position to the other devices (Kinect / VR)
    //
    //------------------------------------------------------------------------
    public void setHololensHeadPositionFlag(bool value)
    {
        canSendHololensPosition = value;
    }

    //------------------------------------------------------------------------
    // Function: setHololensID(int newID)
    //
    // Function sets the hololens ID so it knows of it's own ID when it tries
    // to send movement packages to the other devices
    //
    //------------------------------------------------------------------------
    public void setHololensID(int newID)
    {
        hololensPositionID = newID;
#if !UNITY_EDITOR
        try
        {
            // Create a buffer
            List<byte> messageBuffer = new List<byte>();
                    
            //Debug.Log("adding positions");
            // Add our position
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.position.x));
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.position.y));
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.position.z));

            //Debug.Log("adding rotations");
            // Add our Rotation
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.rotation.eulerAngles.x));
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.rotation.eulerAngles.y));
            messageBuffer.AddRange(BitConverter.GetBytes(headPosition.transform.rotation.eulerAngles.z));
            

            //Debug.Log("Adding ID");
            // Add our ID
            messageBuffer.AddRange(BitConverter.GetBytes(hololensPositionID));

            //the byte[] to send
            byte[] theDataPackage = messageBuffer.ToArray();
            
            
            //Debug.Log("You're done setting up the package");

            dataSender.SendData(theDataPackage, DataSending.HOLO_HEAD_CREATION_FLAG);

        }
        catch(Exception e)
        {
           Debug.Log("Could not send the newly created cordinates to other devices. Error: " + e); 
        }

#endif
        //set the flag so it can send the update position
        setHololensHeadPositionFlag(true);
    }


    //------------------------------------------------------------------------
    // Utility Function to swap endian of a int
    //
    // numberToSwap -- the integer to swap endian, returns a new int with 
    //                 endian swapped.
    //------------------------------------------------------------------------
    public float swapEndianFloat(float numberToSwap)
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
        return BitConverter.ToSingle(bytes, 0);

    }
}