using UnityEngine;
using System.Collections;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;

public class SimulatedWalker : MonoBehaviour {

    private GlobalConfiguration globalConfiguration;
    private RedirectionManager redirectionManager;

    [HideInInspector]
    public MovementManager movementManager;

    const float MINIMUM_DISTANCE_TO_WAYPOINT_FOR_ROTATION = 0.0001f;
    const float ROTATIONAL_ERROR_ACCEPTED_IN_DEGRESS = 1;// If user's angular deviation from target is more than this value, we won't move (until we face the target better) - If you go low sometimes it can stop close to target
    const float EXTRA_WALK_TO_ENSURE_RESET = 0.01f;

    private void Awake()
    {
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        redirectionManager = GetComponentInParent<RedirectionManager>();
        movementManager = GetComponentInParent<MovementManager>();
    }
    // Use this for initialization
    void Start () {
	
	}
	
	// Update is called once per frame

    public void UpdateSimulatedWalker() {
        //experiment is not running
        if (!redirectionManager.globalConfiguration.experimentInProgress)
            return;
        if (redirectionManager.globalConfiguration.avatarIsWalking)
        {
            if (redirectionManager.globalConfiguration.movementController == GlobalConfiguration.MovementController.AutoPilot)
            {                
                if (!redirectionManager.inReset)
                {                    
                    if (!redirectionManager.resetter.IfCollisionHappens()) {
                        if (movementManager.pathSeedChoice == PathSeedChoice.RealUserPath) {
                            GetPosDirAndSet();
                        }
                        else{
                            TurnAndWalkToWaypoint();
                        }                        
                    }                        
                }                
                else
                {//in reset
                    redirectionManager.resetter.SimulatedWalkerUpdate();
                }
            }
            else if (redirectionManager.globalConfiguration.movementController == GlobalConfiguration.MovementController.Keyboard)
            {                
                if (!redirectionManager.inReset)
                {                    
                    if (!redirectionManager.resetter.IfCollisionHappens())
                        redirectionManager.keyboardController.MakeOneStepKeyboardMovement();
                }                
                else
                {//in reset
                    redirectionManager.resetter.SimulatedWalkerUpdate();
                }                
            }
        }
    }
    //calculate position/rotation and set
    public void GetPosDirAndSet() {
        if (movementManager.ifMissionComplete|| movementManager.waypointIterator ==0)
            return;
        var waypointIterator = movementManager.waypointIterator;
        var p = movementManager.waypoints[waypointIterator - 1];
        var q = movementManager.waypoints[waypointIterator];
        var pos = p + (q - p) * (redirectionManager.redirectionTime - movementManager.accumulatedWaypointTime) / (movementManager.samplingIntervals[waypointIterator]);
        var dir = (q - p).normalized;
        transform.position = Utilities.UnFlatten(pos, transform.position.y);
        if (dir.magnitude != 0)
            transform.forward = Utilities.UnFlatten(dir);
    }
    //turn to target then walk to target
    public void TurnAndWalkToWaypoint()
    {
        Vector3 userToTargetVectorFlat;
        float rotationToTargetInDegrees;
        GetDistanceAndRotationToWaypoint(out rotationToTargetInDegrees, out userToTargetVectorFlat);
        
        RotateIfNecessary(rotationToTargetInDegrees, userToTargetVectorFlat);
        GetDistanceAndRotationToWaypoint(out rotationToTargetInDegrees, out userToTargetVectorFlat);
    
        WalkIfPossible(rotationToTargetInDegrees, userToTargetVectorFlat);

    }

    public void RotateIfNecessary(float rotationToTargetInDegrees, Vector3 userToTargetVectorFlat)
    {
        // Handle Rotation To Waypoint
        float rotationToApplyInDegrees = Mathf.Sign(rotationToTargetInDegrees) * Mathf.Min(redirectionManager.GetDeltaTime() * globalConfiguration.rotationSpeed, Mathf.Abs(rotationToTargetInDegrees));

        // Preventing Rotation When At Waypoint By Checking If Distance Is Sufficient        
        if (userToTargetVectorFlat.magnitude > MINIMUM_DISTANCE_TO_WAYPOINT_FOR_ROTATION)
            transform.Rotate(Vector3.up, rotationToApplyInDegrees, Space.World);
    }

    // Rotates rightward in place    
    public void RotateInPlace(float rotateAngle)
    {
        transform.Rotate(Vector3.up, rotateAngle, Space.World);
    }

    

    public void WalkIfPossible(float rotationToTargetInDegrees, Vector3 userToTargetVectorFlat)
    {
        // Handle Translation To Waypoint
        // Luckily once we get near enough to the waypoint, the following condition stops us from shaking in place        
        if (Mathf.Abs(rotationToTargetInDegrees) < ROTATIONAL_ERROR_ACCEPTED_IN_DEGRESS)
        {
            
            // Ensuring we don't overshoot the waypoint, and we don't go out of boundary
            
            float distanceToTravel = Mathf.Min(redirectionManager.GetDeltaTime() * globalConfiguration.translationSpeed, userToTargetVectorFlat.magnitude);
            //Debug.Log("distanceToTravel:" + distanceToTravel + ", redirectionManager.GetDeltaTime():" + redirectionManager.GetDeltaTime());
            transform.Translate(distanceToTravel * Utilities.FlattenedPos3D(redirectionManager.currDir).normalized, Space.World);            
        }
        else
        {//do nothing
            //Debug.Log("Not Travelling");
            //Debug.Log("rotationToWaypointInDegrees: " + rotationToWaypointInDegrees);
        }
    }

    //get rotation and translation vector
    void GetDistanceAndRotationToWaypoint(out float rotationToTargetInDegrees, out Vector3 userToTargetVectorFlat)
    {
        //vector between the avatar to the next target
        userToTargetVectorFlat = Utilities.FlattenedPos3D(redirectionManager.targetWaypoint.position - redirectionManager.currPos);
        //rotation angle needed
        rotationToTargetInDegrees = Utilities.GetSignedAngle(Utilities.FlattenedDir3D(redirectionManager.currDir), userToTargetVectorFlat);
    }

}
