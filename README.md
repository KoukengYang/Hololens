# Hololens
This is the Hololens side of the project, which includes the whole Unity project that runs on the Hololens. This must be run from the Hololens version of Unity in order to properly compile.

AR Project set up list

1. Turn on the router and make sure that it is broadcasting on the 2.4ghz and 5ghz band
	(the default ip of the router is 198.168.0.1_)


2. Connect the server computer (currently the kinect machine) to the router either via ethernet cable or 2.4/5 ghz wifi
	(as of now the ip of the server computer is hardcoded to 198.168.0.2 and it will not work if the server computer ip is different)
	(connecting to hololens is on port 45000, connecting to the vive is on default 45045)

3. Turn on the hololens and connect to the router via the 5ghz wifi band (communication will only go through the 5ghz wifi)
	(the ip of the hololens is hardcoded as 198.162.0.3)

4. Start the server computer unity program. It should debug.log listening on port numbers

5. launch the program on the hololens.

6. Scan the room with the hololens and say "send meshes" when you are ready to sent the room mesh to the server computer.
	(on the hololens side there should be a quick jolt or freeze durng the time the hololens is sending the meshes to the server)

7. Say "set camera" to set the camera on the kinect side. when you set camera, the camera is coded to face the hololens user at the moment.

8. To do real time tracking of the hololens user, say "pineapple" on the hololens to see the hololens avatar on the kinect camera.

9. To drop an object on the hololens side say "set cube", "set sphere", or "set tree" to creat the desired object.

10. On the hololens Air tap to place objects and air tap objects to move objects.
