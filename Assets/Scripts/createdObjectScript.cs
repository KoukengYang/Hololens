//----------------------------------------------------------------------------
// File:        createdObjectScript.cs
// Authors:     Koukeng Yang (Keng)
// DateCreated: July 2016
//
// Notes:       This script is attached to the newly created hologram objects
//              at run time when the objects are created dynamically.
//
//----------------------------------------------------------------------------
using UnityEngine;
using System.Collections;
using HoloToolkit;

public class createdObjectScript : MonoBehaviour
{
    public static float OBJECT_DISTANCE_FROM_USER_ON_MOVE = 1.5f; //1.5 meters from the users gaze

    //a reference to the spatial mapping for spatial mapping masking
    HoloToolkit.Unity.SpatialMappingManager ourSpatialManager;

    //a reference to the object creator to send new cordinates to after movement
    ObjectCreator ourObjectCreator;

    bool isSelected = false;
    bool sendCoord = true;

	// Use this for initialization
	void Start ()
    {
        //find a reference to the spatialMapping object in our scene
        ourSpatialManager = GameObject.Find("SpatialMapping").GetComponent<HoloToolkit.Unity.SpatialMappingManager>();

        //find a reference to the object creator to tell it to send the new move cordinates after we're done moving around
        ourObjectCreator = GameObject.Find("SpatialMapping").GetComponent<ObjectCreator>();

    }
	
	// Update is called once per frame
	void Update ()
    {
        if (isSelected)
        {
            //CODE TO SNAP TO SPATIAL MESH------------------------------------
            //****************************************************************
            //// Do a raycast into the world that will only hit the Spatial Mapping mesh.
            //var headPosition = Camera.main.transform.position;
            //var gazeDirection = Camera.main.transform.forward;

            //RaycastHit hitInfo;
            //***********************************************     *******************************************************************
            //( 1 << ourSpatialManager.PhysicsLayer) means to --> Convert the layer into a mask so it can be used to Raycast against.
            //***********************************************     *******************************************************************
            //if (Physics.Raycast(headPosition, gazeDirection, out hitInfo, 30.0f, ( 1 << ourSpatialManager.PhysicsLayer))) 
            //{
            //    // Move this object to
            //    // where the raycast hit the Spatial Mapping mesh.
            //    this.transform.position = hitInfo.point;

            //    this.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitInfo.normal);
            //}
            //*****************************************************************
            //-----------------------------------------------------------------

            //CODE TO MAKE OBJECT IN FRONT OF USER GAZE------------------------
            //****************************************************************
            var headPosition = Camera.main.transform.position;
            var gazeDirection = Camera.main.transform.forward;

            //put the ojbect in front of the hololens users head
            this.transform.position = headPosition + (gazeDirection * OBJECT_DISTANCE_FROM_USER_ON_MOVE);

            //offset it for the anchor point so we are looking at the middle of it
            Vector3 offset = this.transform.position;
            offset.y = offset.y - .15f;
            this.transform.position = offset;

            //rotate the object to look at the user at all times
            this.transform.LookAt(headPosition);
            //*****************************************************************
            //----------------------------------------------------------------

        }
    }

    //------------------------------------------------------------------------
    // Function: OnSelect()
    //
    // - Function called by the GazeGestureManager when the user 
    //   performs a Select gesture in the hololens
    //
    // - Function flips a flag which allows the hologram that was targeted to
    //   be moved around. When selected again, the hologram will lock in place
    //   and the new cordinates of this hologram will be braodcasted out to
    //   the rest of the other devices (kinect / VR) 
    //------------------------------------------------------------------------
    void OnSelect()
    {
        isSelected = !isSelected; //flip the selected bool

        if (isSelected == false)
        {
            //only get here after you placed the object down
            //Debug.Log("Done Placing the object, now sending new cordinates...");
            //Debug.Log("This object ID is: " + this.name);
            ourObjectCreator.sendObjectMoveCordinates(this.name);
        }
    }


}
