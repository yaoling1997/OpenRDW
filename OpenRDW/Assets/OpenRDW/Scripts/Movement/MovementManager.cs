using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;
using AvatarInfo = ExperimentSetup.AvatarInfo;

public class MovementManager : MonoBehaviour {
    [HideInInspector]
    public int avatarId;//start from 0

    [HideInInspector]
    public GlobalConfiguration generalManager;

    [HideInInspector]
    public RedirectionManager redirectionManager;

    [Tooltip("Custom waypoints file path")]
    [SerializeField]
    private string waypointsFilePath;

    [Tooltip("Sampling time intervals between waypoints, used when Path Seed Choice == Real User Path")]
    [SerializeField]
    private string samplingIntervalsFilePath;

    [HideInInspector]
    public List<Vector2> waypoints;//waypoints for simulation/collection

    [HideInInspector]
    public List<float> samplingIntervals;//sampling rate read from record files

    [Tooltip("The path seed used for generating waypoints")]
    [SerializeField]
    public PathSeedChoice pathSeedChoice;

    [HideInInspector]
    public InitialConfiguration initialConfiguration;//user's start point and direction

    [HideInInspector]
    public int waypointIterator = 0;

    [HideInInspector]
    public float accumulatedWaypointTime;//only take effect when pathChoice == realUserPath

    [HideInInspector]
    public SimulatedWalker simulatedWalker;// represent the user's position
    [HideInInspector]
    public bool ifInvalid;//if this avatar becomes invalid (stay at the same position for a long time, exceeds the given time)
    [HideInInspector]
    public bool ifMissionComplete;//If finish this given path
    [HideInInspector]
    public HeadFollower headFollower;//headFollower

    [HideInInspector]
    public List<Transform> otherAvatarRepresentations;//Other avatars' representations

    //if this avatar is visible
    [HideInInspector]
    public bool ifVisible;

    [HideInInspector]
    public List<GameObject> bufferRepresentations;//buffer gameobjects

    [HideInInspector]
    public List<GameObject> avatarBufferRepresentations;//gameobject of avatar Buffer, index represents the buffer of the avatar with avatarId

    [HideInInspector]
    public Camera cameraTopReal;//stay still relative to the physical space

    [HideInInspector]
    public Transform plane;
    private Transform obstacleParent;
    private Transform bufferParent;

    private GlobalConfiguration globalConfiguration;

    private GameObject trackingSpace;//tracing space gameobject

    private void Awake()
    {
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        ifVisible = true;
        generalManager = GetComponentInParent<GlobalConfiguration>();

        redirectionManager = GetComponent<RedirectionManager>();
        simulatedWalker = transform.Find("Simulated Avatar").Find("Head").GetComponent<SimulatedWalker>();
        headFollower = transform.Find("Body").GetComponent<HeadFollower>();

        cameraTopReal = transform.Find("Real Top View Cam").GetComponent<Camera>();

        trackingSpace = transform.Find("Tracking Space").gameObject;
        plane = trackingSpace.transform.Find("Plane");
        obstacleParent = plane.Find("ObstacleParent");
        bufferParent = plane.Find("BufferParent");

        bufferRepresentations = new List<GameObject>();
        avatarBufferRepresentations = new List<GameObject>();
    }

    public void ChangeColor(Color newColor) {
        transform.Find("Body").GetComponent<HeadFollower>().ChangeColor(newColor);
    }

    public void ChangeTrackingSpaceVisibility(bool ifVisible){
        trackingSpace.SetActive(ifVisible);
    }
    public void SwitchPersonView(bool ifFirstPersonView) {
        redirectionManager.simulatedHead.Find("1st Person View").gameObject.SetActive(ifFirstPersonView);
        redirectionManager.simulatedHead.Find("3rd Person View").gameObject.SetActive(!ifFirstPersonView);
    }

    //set avatar's visibility (avatar, waypoint...)
    public void SetVisibility(bool ifVisible) {
        this.ifVisible = ifVisible;

        foreach (var mr in GetComponentsInChildren<MeshRenderer>(true)) {
            mr.enabled = ifVisible;
        }

        foreach (var mr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            mr.enabled = ifVisible;


        //if waypoint visible
        if (redirectionManager.targetWaypoint != null)
            redirectionManager.targetWaypoint.GetComponent<MeshRenderer>().enabled = ifVisible;

        //if camera is working
        foreach (var cam in GetComponentsInChildren<Camera>())
        {
            cam.enabled = ifVisible;
        }
    }

    //one step movement
    public void MakeOneStepMovement()
    {
        //skip invalid data
        if (ifInvalid)
            return;

        UpdateSimulatedWaypointIfRequired();
        simulatedWalker.UpdateSimulatedWalker();
        if (IfInvalidData())
        {
            Debug.LogError(string.Format("InvalidData! experimentIterator = {0}, userid = {1}", generalManager.experimentIterator, avatarId));
            ifInvalid = true;            
        }
    }

    void InstantiateSimulationPrefab()
    {
        if (redirectionManager.targetWaypoint != null) {
            Destroy(redirectionManager.targetWaypoint.gameObject);
        }
        Transform waypoint = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
        waypoint.gameObject.layer = LayerMask.NameToLayer("Waypoint");
        Destroy(waypoint.GetComponent<SphereCollider>());
        redirectionManager.targetWaypoint = waypoint;
        waypoint.name = "Simulated Waypoint";
        waypoint.position = 1.2f * Vector3.up + 1000 * Vector3.forward;
        waypoint.localScale = 0.3f * Vector3.one;
        waypoint.GetComponent<Renderer>().material.color = new Color(0, 1, 0);
        waypoint.GetComponent<Renderer>().material.SetColor("_EmissionColor", new Color(0, 0.12f, 0));
    }


    //get new waypoints
    public void InitializeWaypointsPattern() {
        generalManager.GenerateWaypoints(pathSeedChoice, waypointsFilePath, samplingIntervalsFilePath, out waypoints, out samplingIntervals);
    }

    //check if need to update waypoint
    void UpdateSimulatedWaypointIfRequired()
    {
        //experiment is not in progress
        if (!generalManager.experimentInProgress)
            return;
        
        if (pathSeedChoice == PathSeedChoice.RealUserPath)
        {
            var redirectionTime = redirectionManager.redirectionTime;
            var samplingInterval = GetSamplingIntervalByWaypointIterator(waypointIterator);
            while (!ifMissionComplete && waypointIterator < waypoints.Count && redirectionTime > accumulatedWaypointTime + samplingInterval)
            {
                accumulatedWaypointTime += samplingInterval;
                UpdateWaypoint();
                samplingInterval = GetSamplingIntervalByWaypointIterator(waypointIterator);
            }
        }
        else {
            if ((redirectionManager.currPos - Utilities.FlattenedPos3D(redirectionManager.targetWaypoint.position)).magnitude < generalManager.distanceToWaypointThreshold)
            {
                UpdateWaypoint();
            }
        }
    }
    //0 index represents the start point, so the corresponding sampling Interval equals to 0
    public float GetSamplingIntervalByWaypointIterator(int waypointIterator) {
        return waypointIterator == 0 ? 0 : samplingIntervals[waypointIterator];
    }

    //check if this data becomes invalid, break the trial if invalid, (invalid: reset exceeds the upper limit, stuck in a same position for too long)
    public bool IfInvalidData()
    {
        return generalManager.statisticsLogger.IfResetCountExceedLimit(avatarId) || redirectionManager.IfWaitTooLong();
    }


    //get next waypoint
    public void UpdateWaypoint()
    {

        //Debug.Log(string.Format("waypoint: {0}/{1}", waypointIterator, waypoints.Count));
        if (waypointIterator == waypoints.Count - 1)
        {
            ifMissionComplete = true;
        }
        else
        {
            waypointIterator++;
            redirectionManager.targetWaypoint.position = new Vector3(waypoints[waypointIterator].x, redirectionManager.targetWaypoint.position.y, waypoints[waypointIterator].y);
        }
    }

    //align the recorded waypoints to the given point and direction,     
    public List<Vector2> GetRealWaypoints(List<Vector2> preWaypoints, Vector2 initialPosition, Vector2 initialForward, out float sumOfDistances, out float sumOfRotations)
    {
        sumOfDistances = 0;
        sumOfRotations = 0;
        var recordedWaypoints = preWaypoints;
        var deltaPos = initialPosition - VirtualPathGenerator.defaultStartPoint;
        var newWaypoints = new List<Vector2>();
        var pos = initialPosition;
        var forward = initialForward;
        foreach (var p in recordedWaypoints)
        {
            var newPos = p + deltaPos;
            newWaypoints.Add(newPos);

            sumOfDistances += (newPos - pos).magnitude;
            sumOfRotations += Vector2.Angle(forward, newPos - pos);

            forward = (newPos - pos).normalized;
            pos = newPos;
        }

        //align waypoint[1] to the init direction, rotate other waypoints
        if (generalManager.alignToInitialForward) {
            var virtualDir = Vector2.up;
            if (generalManager.firstWayPointIsStartPoint)
            {
                virtualDir = newWaypoints[1] - initialPosition;
            }
            else {
                virtualDir = newWaypoints[0] - initialPosition;
            }
            var rotAngle = Utilities.GetSignedAngle(Utilities.UnFlatten(virtualDir), Utilities.UnFlatten(initialForward));
            for (int i = 0; i < newWaypoints.Count; i++) {
                var vec = Utilities.RotateVector(newWaypoints[i] - initialPosition, rotAngle);                
                newWaypoints[i] = initialPosition + vec;
            }
        }
        return newWaypoints;
    }

    //Get Current avatar's info(waypoint,redirector,resetter)
    public AvatarInfo GetCurrentAvatarInfo()
    {
        var rm = redirectionManager;
        return new AvatarInfo(rm.redirectorType, rm.resetterType, pathSeedChoice, waypoints, waypointsFilePath, samplingIntervals, samplingIntervalsFilePath, initialConfiguration);
    }
    //Update other avatar representations
    public void InitializeOtherAvatarRepresentations() {
        //Destroy representations of last trial
        if (otherAvatarRepresentations != null)
            foreach (var oldRepresentation in otherAvatarRepresentations)
                Destroy(oldRepresentation.gameObject);

        //initialize other avatars' representations
        otherAvatarRepresentations = new List<Transform>();                
        for (int i = 0; i < generalManager.redirectedAvatars.Count; i++)
        {
            var representation = globalConfiguration.CreateAvatar(transform, i);

            otherAvatarRepresentations.Add(representation.transform);
            var avatarColor = globalConfiguration.avatarColors[i];
            foreach (var mr in representation.GetComponentsInChildren<MeshRenderer>())
            {
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = avatarColor;
            }
            foreach (var mr in representation.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                mr.material = new Material(Shader.Find("Standard"));
                mr.material.color = avatarColor;                
            }

            //visualize buffer
            var bufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(new List<Vector2> { Vector2.zero }, false, generalManager.RESET_TRIGGER_BUFFER);
            var obj = AddBufferMesh(bufferMesh, representation.transform);

            //hide
            if (i == avatarId) {
                representation.SetActive(false);
                obj.SetActive(false);
            }
            avatarBufferRepresentations.Add(obj);
        }
    }

    //Init visualization of buffer
    public void InitializeBuffers() {
        if (bufferRepresentations != null)
            foreach (var buffer in bufferRepresentations)
                Destroy(buffer.gameObject);
        avatarBufferRepresentations = new List<GameObject>();
        bufferRepresentations = new List<GameObject>();

        var bufferParent = new GameObject().transform;
        bufferParent.SetParent(this.bufferParent.parent);
        bufferParent.name = this.bufferParent.name;
        bufferParent.localPosition = this.bufferParent.localPosition;
        bufferParent.rotation = this.bufferParent.rotation;
        Destroy(this.bufferParent.gameObject);
        this.bufferParent = bufferParent;
    }

    //need to reload data for each trial
    public void LoadData(int avatarId, AvatarInfo avatar) {
        var rm = redirectionManager;
        this.avatarId = avatarId;
        rm.redirectorType = avatar.redirector;
        rm.resetterType = avatar.resetter;
        pathSeedChoice = avatar.pathSeedChoice;
        waypoints = avatar.waypoints;
        waypointsFilePath = avatar.waypointsFilePath;
        samplingIntervals = avatar.samplingIntervals;
        samplingIntervalsFilePath = avatar.samplingIntervalsFilePath;
        if (generalManager.movementController.Equals(GlobalConfiguration.MovementController.HMD))
        {
            //HMD mode, set as current position and direction
            initialConfiguration = new InitialConfiguration(Utilities.FlattenedPos2D(rm.headTransform.position), Utilities.FlattenedDir2D(redirectionManager.headTransform.forward));
        }
        else
        {
            //auto simulation and keyboard controll mode, apply initial positions and directions
            initialConfiguration = avatar.initialConfiguration;
        }        
        
        InitializeBuffers();
        
        InitializeOtherAvatarRepresentations();

        ifInvalid = false;
        ifMissionComplete = false;

        float sumOfDistances, sumOfRotations;
        //Set virtual path
        waypoints = GetRealWaypoints(waypoints, initialConfiguration.initialPosition, initialConfiguration.initialForward, out sumOfDistances, out sumOfRotations);

        InstantiateSimulationPrefab();

        rm.redirectorChoice = RedirectionManager.RedirectorToRedirectorChoice(rm.redirectorType);
        rm.resetterChoice = RedirectionManager.ResetterToResetChoice(rm.resetterType);

        //Set priority, large priority call early
        redirectionManager.priority = this.avatarId;

        // Set First Waypoint Position and Enable It
        redirectionManager.targetWaypoint.position = new Vector3(waypoints[0].x, redirectionManager.targetWaypoint.position.y, waypoints[0].y);
        waypointIterator = 0;
        accumulatedWaypointTime = 0;

        // Enabling/Disabling Redirectors
        redirectionManager.UpdateRedirector(rm.redirectorType);
        redirectionManager.UpdateResetter(rm.resetterType);

        // Stop Trail Drawing and Delete Virtual Path
        redirectionManager.trailDrawer.enabled = false;

        // Setup Trail Drawing
        redirectionManager.trailDrawer.enabled = true;
        // Enable Waypoint
        redirectionManager.targetWaypoint.gameObject.SetActive(true);

        // Resetting User and World Positions and Orientations
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        // ESSENTIAL BUG FOUND: If you set the user first and then the redirection recipient, then the user will be moved, so you have to make sure to do it afterwards!
        //Debug.Log("Target User Position: " + setup.initialConfiguration.initialPosition.ToString("f4"));

        redirectionManager.headTransform.position = Utilities.UnFlatten(initialConfiguration.initialPosition, redirectionManager.headTransform.position.y);
        //Debug.Log("Result User Position: " + redirectionManager.userHeadTransform.transform.position.ToString("f4"));
        redirectionManager.headTransform.rotation = Quaternion.LookRotation(Utilities.UnFlatten(initialConfiguration.initialForward), Vector3.up);

        redirectionManager.Initialize();//initialize when restart a experiment 
        
        redirectionManager.trailDrawer.BeginTrailDrawing();

        headFollower.CreateAvatarViualization();
        var avatarColors = globalConfiguration.avatarColors;
        ChangeColor(avatarColors[avatarId % avatarColors.Length]);
    }
    
    public void GenerateTrackingSpaceMesh(List<Vector2> trackingSpacePoints, List<List<Vector2>> obstaclePolygons)
    {
        var trackingSpaceMesh = TrackingSpaceGenerator.GeneratePolygonMesh(trackingSpacePoints);
        
        //tracking space mesh
        plane.GetComponent<MeshFilter>().mesh = trackingSpaceMesh;

        var obstacleParent = new GameObject().transform;
        obstacleParent.SetParent(this.obstacleParent.parent);
        obstacleParent.name = this.obstacleParent.name;
        obstacleParent.localPosition = this.obstacleParent.localPosition;
        obstacleParent.rotation = this.obstacleParent.rotation;
        Destroy(this.obstacleParent.gameObject);
        this.obstacleParent = obstacleParent;

        TrackingSpaceGenerator.GenerateObstacleMesh(obstaclePolygons, obstacleParent, generalManager.obstacleColor, generalManager.if3dObstacle, generalManager.obstacleHeight);

        //buffer mesh
        var trackingSpaceBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(trackingSpacePoints, true, generalManager.RESET_TRIGGER_BUFFER);
        AddBufferMesh(trackingSpaceBufferMesh);
        foreach (var obstaclePoints in obstaclePolygons) {
            var obstacleBufferMesh = TrackingSpaceGenerator.GenerateBufferMesh(obstaclePoints, false, generalManager.RESET_TRIGGER_BUFFER);
            AddBufferMesh(obstacleBufferMesh);
        }
    }
    
    public GameObject AddBufferMesh(Mesh bufferMesh) {

        var obj = new GameObject("bufferMesh" + bufferRepresentations.Count);
        obj.transform.SetParent(bufferParent);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.rotation = Quaternion.identity;

        obj.AddComponent<MeshFilter>().mesh = bufferMesh;
        var mr = obj.AddComponent<MeshRenderer>();
        mr.material = new Material(generalManager.transparentMat);
        
        //MaterialExtensions.ToFadeMode(mr.material);        
        mr.material.color = generalManager.bufferColor;
        //Debug.Log("generalManager.bufferColor: " + generalManager.bufferColor);

        bufferRepresentations.Add(obj);
        return obj;
    }
    public GameObject AddBufferMesh(Mesh bufferMesh,Transform followedObj) {
        var obj = AddBufferMesh(bufferMesh);
        var hf = obj.AddComponent<HorizontalFollower>();
        hf.followedObj = followedObj;
        return obj;
    }
    //visualization relative, update other avatar representations...
    public void UpdateVisualizations() {
        //update avatar
        headFollower.UpdateManually();
        //update trail   
        redirectionManager.trailDrawer.UpdateManually();
        for (int i = 0; i < otherAvatarRepresentations.Count; i++) {            
            if (i == avatarId)
                continue;
            var us = generalManager.redirectedAvatars[i];
            var rm = us.GetComponent<RedirectionManager>();
            otherAvatarRepresentations[i].localPosition = rm.currPosReal;
            otherAvatarRepresentations[i].localRotation = Quaternion.LookRotation(rm.currDirReal, Vector3.up);            
        }
    }

    public void SetBufferVisibility(bool ifVisible) {
        for (int i = 0; i < bufferRepresentations.Count; i++) {
            bufferRepresentations[i].SetActive(ifVisible);
        }
        avatarBufferRepresentations[avatarId].SetActive(false);
    }
}

