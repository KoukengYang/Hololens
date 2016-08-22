//----------------------------------------------------------------------------
// File:        ObjectCreator.cs
// Authors:     Koukeng Yang (Keng), Michael Tanaya, Taran Christensen
// DateCreated: July 2016
//
// Note:        This script keeps track of the HASHMAP which holds
//              the information of all holograms in the program. This script
//              also spawns new holograms and updates the hashmap based on 
//              created objects from the other devices. (kinect / VR)
//----------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;

public class ObjectCreator : MonoBehaviour
{
    public static int OBJECT_CUBE = 1;
    public static int OBJECT_SPHERE = 2;
    public static int OBJECT_TREE = 3;

    //change this number to match the max number of objects we can create, 
    //e.g. we have 3 here because currently we can only make cubes,spheres,and trees.
    public static int MAX_NUM_OF_OBJECT_TYPES = 3;

    //by default create cubes
    private int curSelectedObjectToCreate = 1; 

    //Reference to the networkSender
    public netWorkManager theNetworkManager;

    //The ID generator
    public static System.Random numGenerator = new System.Random();

    //THE HASH MAP TO STORE USED ID
    public static Dictionary<int, GameObject> objects = new Dictionary<int, GameObject>();

    //HASHMAP lock
    public static System.Object IDListLock = new System.Object();

    //a reference to this current terrain collider to fix the offset
    //MeshCollider thisCollider;

    //// Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {

    }

    // Function: OnSelect()
    //
    // - Function called by the GazeGestureManager when the user 
    //   performs a Select gesture in the hololens
    //
    // - Function creates an object specified by the curSelectedObjectToCreate
    //   variable at the spot the user is looking when the user does a select
    //   command in the hololens.
    //------------------------------------------------------------------------
    public void OnSelect()
    {
        //grab the current positions of the users head
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        //racasthit info holder
        RaycastHit hitInfo;

        //put the raycast intto the hitInfo
        Physics.Raycast(headPosition, gazeDirection, out hitInfo);

        if (curSelectedObjectToCreate == OBJECT_CUBE)
        {
            //create a cube at the spot hit
            GameObject cube = Instantiate(Resources.Load("Cube")) as GameObject;
            //move cube to spot
            cube.transform.Translate(hitInfo.point);
            //rotate cube to face upwards
            cube.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
         

            //add this to the list of objects and send it to other devices
            this.addCreatedObjectToList(cube, OBJECT_CUBE);
        }
        else if (curSelectedObjectToCreate == OBJECT_SPHERE)
        {
            //create a sphere at the spot hit
            GameObject sphere = Instantiate(Resources.Load("Sphere")) as GameObject;
            //move tree to correct spot
            sphere.transform.Translate(hitInfo.point);
            //rotate tree to face upwards
            sphere.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);

            //add this to the list of objects and send it to other devices
            this.addCreatedObjectToList(sphere, OBJECT_SPHERE);
        }
        else if (curSelectedObjectToCreate == OBJECT_TREE)
        {
            //create a tree at the spot hit
            GameObject tree = Instantiate(Resources.Load("Tree")) as GameObject;
            //move tree to correct spot
            tree.transform.Translate(hitInfo.point);
            //rotate tree to face upwards
            tree.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);

            //add this to the list of objects and send it to other devices
            this.addCreatedObjectToList(tree, OBJECT_TREE);
        }
        else
        {
            Debug.Log("UNKNOWN OBJECT TO CREATE IN OBJECT CREATOR. OBJECT ID IS: " + curSelectedObjectToCreate);
        }

        //Transform treeTransform = Instantiate(tree, hitInfo.point, Quaternion.FromToRotation(Vector3.up, hitInfo.normal)) as Transform;
        //GameObject treeObject = treeTransform.gameObject;   
    }


    //------------------------------------------------------------------------
    // Function: addCreatedObjectToList(GameObject objectToAdd, int objectType)
    //
    // - Function adds the newly created object to the hashtable for look up
    //   and information storing.
    //
    // - ***Function is used when creating a new object on the hololens end***
    //------------------------------------------------------------------------
    private void addCreatedObjectToList(GameObject objectToAdd, int objectType)
    {
        if (objectToAdd == null)
        {
            Debug.Log("INCOMING OBJECT IS NULL");
        }

        int newID;
        //set the object's new ID
        lock (IDListLock)
        {
            //we start by generating a random ID to test if that ID exists in our
            //HASHMAP. If and ID already exists, find a new random ID and test until we
            //find an ID that has not been assigned yet and assign it to the incoming objectToAdd.

            newID = numGenerator.Next();
            GameObject throwAway;
            while (ObjectCreator.objects.TryGetValue(newID, out throwAway))
            {
                newID = numGenerator.Next();
            }

            //Debug.Log("GOOD ID FOUND, ID IS: " + newID);

            //add the name to the object
            objectToAdd.name = newID.ToString();

            //after this point the object ID should be set
            //Debug.Log("About to add to the hash table");

            if (objects == null)
            {
                Debug.Log("the hash table is null,..");
            }

            ObjectCreator.objects.Add(newID, objectToAdd);
            //Debug.Log("adding to the hash completed");


        }

        //send the coords to the other devices
        theNetworkManager.sendObjectCoords(objectToAdd, objectType, newID);
    }


    //------------------------------------------------------------------------
    // Function: addHololensPositionToList(GameObject CameraObjectToAdd
    //
    // - Function adds the hololens ID to the list so that it's ID won't
    //   clash with the other objects created. the hololens viewport is a
    //   camera.
    //------------------------------------------------------------------------
    public void addHololensPositionToList(GameObject CameraObjectToAdd)
    {
        if (CameraObjectToAdd == null)
        {
            Debug.Log("INCOMING OBJECT IS NULL");
        }

        int newID;
        //set the object's new ID
        lock (IDListLock)
        {
            newID = numGenerator.Next();
            GameObject throwAway;
            while (ObjectCreator.objects.TryGetValue(newID, out throwAway))
            {
                newID = numGenerator.Next();
            }

            //Debug.Log("GOOD ID FOUND, ID IS: " + newID);

            //add the name to the object
            CameraObjectToAdd.name = newID.ToString();

            //after this point the object ID should be set
            //Debug.Log("About to add to the hash table");

            if (objects == null)
            {
                Debug.Log("the hash tabel is null,..");
            }

            ObjectCreator.objects.Add(newID, CameraObjectToAdd);
            //Debug.Log("adding to the hash completed");



            theNetworkManager.setHololensID(newID);
        }
    }


    //------------------------------------------------------------------------
    // Function: sendObjectMoveCordinates(string objectID)
    //
    // - Function tells the network manager to send the updated cordinates 
    //   of the object specified by the objectID to the other devices
    //------------------------------------------------------------------------
    public void sendObjectMoveCordinates(string objectID)
    {
        //tell the network manager to send the updated cordinates of the object specified by objectID
        theNetworkManager.sendObjectMoveCoords(objectID);
    }

    //------------------------------------------------------------------------
    // Function: addToListOfObjects(int newID, GameObject objectToAdd)
    //
    // - Function adds the object to the hashtable for look up
    //   and information storing.
    //
    // - ***Function is used when receiving object create packets from the
    //   other devices (kinect and VR)***
    //------------------------------------------------------------------------
    public void addToListOfObjects(int newID, GameObject objectToAdd)
    {
        lock (IDListLock)
        {
            GameObject throwAway;
            //if it hashes to a place without an object
            if ((objects.TryGetValue(newID, out throwAway))) //returns false if it hash to a null place, returns true if something already exists there
            {
             //it already has an object at this location in the hashtable
             //this should not happen based on object creation but just in case...
                Debug.Log("ERROR, OBJECT ALREADY EXISTS AT THIS HASH LOCATION... ID = " + newID);
            }
            else
            {
                //nothing exists at the spot in the hashtable and the ID is
                //good so set this spot in the hash table
                objects.Add(newID, objectToAdd);
            }
        }
    }

    //------------------------------------------------------------------------
    // Function: setObjectToCreate(int objectIDToSet)
    //
    // - Function sets which object to create when the user does a select
    //   gesture in the hololens
    //------------------------------------------------------------------------
    public void setObjectToCreate(int objectIDToSet)
    {        
        //checking to see if the objectIDToSet is valid
        //our object ID starts at 1 (which is the OBJECT_CUBE flag)
        if ((objectIDToSet > 0) && (objectIDToSet <= MAX_NUM_OF_OBJECT_TYPES))
        {
            //since the object to create ID to set is valid, we set it
            curSelectedObjectToCreate = objectIDToSet;
        }   
    }

    //------------------------------------------------------------------------
    // Function: sendProjectorCordinates()
    //
    // - Function sends the cordinates of the projector to the kinect so that
    //   the kinect knows where to place the camera object
    //------------------------------------------------------------------------
    public void sendProjectorCordinates()
    {
        //sets the projector camera on the kinect side, DOES NOT ROTATE CAMERA

        //grab the current positions of the users head
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        //racasthit info holder
        RaycastHit hitInfo;

        //put the raycast intto the hitInfo
        Physics.Raycast(headPosition, gazeDirection, out hitInfo);


        //create new object at that location
        GameObject spotHolder = new GameObject();

        //shift the camera to that spot facing away from the surface
        spotHolder.transform.position = hitInfo.point;
        spotHolder.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal); ;

        //send the informatuon to other devices
        //theNetworkManager.sendCameraPosition(spotHolder);***************************************************NOT DONE IMPLEMENTED
    }

    public void sendCamera1Cordinates()
    {
        //sets the projector camera1 on the kinect side to point at the hololens user, DOES NOT ROTATE CAMERA

        //grab the current positions of the users head
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        //racasthit info holder
        RaycastHit hitInfo;

        //put the raycast intto the hitInfo
        Physics.Raycast(headPosition, gazeDirection, out hitInfo);

        //search for camera representation object
        GameObject spotHolder = GameObject.Find("Camera1");

        //create new object at that location if the camera doesn't exists yet
        if (spotHolder == null)
        {
            spotHolder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spotHolder.name = "Camera1";
            spotHolder.transform.localScale = new Vector3(.3f,.3f,.3f);
        }

        //shift the camera to that spot facing away from the surface
        spotHolder.transform.position = hitInfo.point;
        spotHolder.transform.LookAt(headPosition);

        //send the informatuon to other devices
        theNetworkManager.sendCameraPosition(spotHolder.transform.position, spotHolder.transform.rotation, 1);
    }

    public void sendCamera2Cordinates()
    {
        //sets the projector camera on the kinect side, DOES NOT ROTATE CAMERA //NOT WORKING AT THE MOMENT SINCE ITS A REPLICA OF SET CAMERA 1

        //grab the current positions of the users head
        var headPosition = Camera.main.transform.position;
        var gazeDirection = Camera.main.transform.forward;

        //racasthit info holder
        RaycastHit hitInfo;

        //put the raycast intto the hitInfo
        Physics.Raycast(headPosition, gazeDirection, out hitInfo);

        //search for camera representation object
        GameObject spotHolder = GameObject.FindGameObjectWithTag("Camera2"); ;

        //create new object at that location if the camera doesn't exists yet
        if (spotHolder == null)
        {
            spotHolder = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spotHolder.tag = "Camera2";
            spotHolder.transform.localScale = new Vector3(.3f, .3f, .3f);
        }

        //shift the camera to that spot facing away from the surface
        spotHolder.transform.position = hitInfo.point;
        spotHolder.transform.rotation = Quaternion.FromToRotation(Vector3.one, hitInfo.normal); ;

        //send the information to other devices
        theNetworkManager.sendCameraPosition(spotHolder.transform.position, spotHolder.transform.rotation, 2);
    }



}
