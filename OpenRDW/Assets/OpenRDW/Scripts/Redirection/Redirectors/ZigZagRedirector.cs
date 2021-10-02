using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ZigZagRedirector : Redirector
{
    [SerializeField]
    [Tooltip("Two points parented to RedirectedUser that are targets in the real world. RealTarget0 is the starting point.")]
    public Transform realTarget0, realTarget1;

    [SerializeField]
    Vector3 RealTarget0DefaultPosition = Vector3.zero, RealTarget1DefaultPosition = new Vector3(3f, 0, 3f);

    /// <summary>
    ///  How close you need to get to the waypoint for it to be considered reached.
    /// </summary>
    float WAYPOINT_UPDATE_DISTANCE = 0.4f;
    /// <summary>
    /// How slow you need to be walking to trigger next waypoint when in proximity to current target.
    /// </summary>
    float SLOW_DOWN_VELOCITY_THRESHOLD = 0.25f;

    bool headingToTarget0 = false;
    int waypointIndex = 1;

    bool initialized = false;

    /**
     * Two big realizations:
     * 1. Expecting curvature to do most of the work is dangerous because when you do that, you'll need more than 180 in the real world for the next waypoint!
     * 2. When you want curvature to do the work, you're not planning correctly. You actually want more rotation then you think. Double actually. If you look at the arc, you end up rotating inward at the end, and you actually peak at the center, and that's when you are aiming in the direction of the line that connects the two real targets
     * So the best thing really to do is to put as much work as possible on rotation, and if there's anything left crank up curvature to max until goal is reached.
     */

    void Initialize()
    {
        
        var waypoints = redirectionManager.movementManager.waypoints;
        Vector3 point0 = Utilities.UnFlatten(waypoints[0]);
        Vector3 point1 = Utilities.UnFlatten(waypoints[1]);

        if (realTarget0 == null)
        {
            realTarget0 = InstantiateDefaultRealTarget(0, RealTarget0DefaultPosition);
            realTarget1 = InstantiateDefaultRealTarget(1, RealTarget1DefaultPosition);
        }

        Vector3 realDesiredDirection =  Utilities.FlattenedDir3D(Utilities.GetRelativePosition(realTarget1.position, this.transform) - Utilities.GetRelativePosition(realTarget0.position, this.transform));
        Quaternion pointToPointRotation = Quaternion.LookRotation(point1 - point0, Vector3.up);
        Quaternion desiredDirectionToForwardRotation = Quaternion.FromToRotation(realDesiredDirection, Vector3.forward);
        Quaternion desiredRotation = desiredDirectionToForwardRotation * pointToPointRotation;

        Vector3 pinnedPointRelativePosition = Utilities.FlattenedPos3D(Utilities.GetRelativePosition(realTarget0.position, this.transform));
        Vector3 pinnedPointPositionRotationCorrect = desiredRotation * pinnedPointRelativePosition;

        if (redirectionManager.globalConfiguration.movementController == GlobalConfiguration.MovementController.AutoPilot)
        {
            WAYPOINT_UPDATE_DISTANCE = 0.1f;
            SLOW_DOWN_VELOCITY_THRESHOLD = 100f;
        }

    }


    void UpdateWaypoint()
    {
        var waypoints = redirectionManager.movementManager.waypoints;
        bool userIsNearTarget = Utilities.FlattenedPos3D(redirectionManager.currPos - Utilities.UnFlatten(waypoints[waypointIndex])).magnitude < WAYPOINT_UPDATE_DISTANCE;
        bool userHasSlownDown = redirectionManager.deltaPos.magnitude / redirectionManager.GetDeltaTime() < SLOW_DOWN_VELOCITY_THRESHOLD;
        bool userHasMoreWaypointsLeft = waypointIndex < waypoints.Count - 1;
        if (userIsNearTarget && userHasSlownDown && userHasMoreWaypointsLeft && !redirectionManager.inReset)
        {
            waypointIndex++;
            headingToTarget0 = !headingToTarget0;
            //Debug.LogWarning("WAYPOINT UDPATED");
        }
    }

    public override void InjectRedirection()
    {
        
        if (!initialized)
        {
            Initialize();
            initialized = true;
        }

        UpdateWaypoint();

        Vector3 virtualTargetPosition;
        Vector3 realTargetPosition;
        Vector3 realTargetPositionRelative;
        float angleToRealTarget;
        Vector3 userToVirtualTarget;
        float angleToVirtualTarget;
        Vector3 userToRealTarget;
        float distanceToRealTarget;
        float expectedRotationFromCurvature;
        float requiredAngleInjection;
        float requiredTranslationInjection;

        float g_c;
        float g_r;
        float g_t;

        var waypoints = redirectionManager.movementManager.waypoints;
        virtualTargetPosition = Utilities.UnFlatten(waypoints[waypointIndex]);
        realTargetPosition = headingToTarget0 ? Utilities.FlattenedPos3D(realTarget0.position) : Utilities.FlattenedPos3D(realTarget1.position);
        realTargetPositionRelative = headingToTarget0 ? Utilities.FlattenedPos3D(Utilities.GetRelativePosition(realTarget0.position, this.transform)) : Utilities.FlattenedPos3D(Utilities.GetRelativePosition(realTarget1.position, this.transform));
        angleToRealTarget = Utilities.GetSignedAngle(redirectionManager.currDir, realTargetPosition - redirectionManager.currPos);
        angleToVirtualTarget = Utilities.GetSignedAngle(redirectionManager.currDir, virtualTargetPosition - redirectionManager.currPos);
        distanceToRealTarget = (realTargetPositionRelative - redirectionManager.currPosReal).magnitude;
        userToVirtualTarget = virtualTargetPosition - redirectionManager.currPos;
        userToRealTarget = realTargetPosition - redirectionManager.currPos;        
        requiredAngleInjection = Utilities.GetSignedAngle(userToRealTarget, userToVirtualTarget);
        
        float minimumRealTranslationRemaining = userToVirtualTarget.magnitude / (1 + redirectionManager.globalConfiguration.MAX_TRANS_GAIN);
        float minimumRealRotationRemaining = angleToVirtualTarget; // / (1 + redirectionManager.MIN_ROT_GAIN);

        // This can slightly be improved by expecting more from rotation when you know the user is rotating in a direction that now requires positive rotation gain instead!
        float expectedRotationFromRotationGain = Mathf.Sign(requiredAngleInjection) * Mathf.Min(Mathf.Abs(requiredAngleInjection), Mathf.Abs(minimumRealRotationRemaining * redirectionManager.globalConfiguration.MIN_ROT_GAIN));
        float remainingRotationForCurvatureGain = requiredAngleInjection - expectedRotationFromRotationGain;
        expectedRotationFromCurvature = Mathf.Sign(requiredAngleInjection) * Mathf.Min(minimumRealTranslationRemaining * (Mathf.Rad2Deg / redirectionManager.globalConfiguration.CURVATURE_RADIUS), Mathf.Abs(2 * remainingRotationForCurvatureGain));

        //requiredTranslationInjection = distanceToTarget - distanceToRealTarget;
        requiredTranslationInjection = (realTargetPosition - virtualTargetPosition).magnitude;

        g_c = distanceToRealTarget < 0.1f ? 0 : (expectedRotationFromCurvature / minimumRealTranslationRemaining); // Rotate in the opposite direction so when the user counters the curvature, the intended direction is achieved
        g_r = distanceToRealTarget < 0.1f || Mathf.Abs(angleToRealTarget) < Mathf.Deg2Rad * 1 ? 0 : expectedRotationFromRotationGain / Mathf.Abs(minimumRealRotationRemaining);
        g_t = distanceToRealTarget < 0.1f ? 0 : requiredTranslationInjection / distanceToRealTarget;

        // New Secret Sauce! Focusing on alignment!
        // Determine Translation Gain Sign and intensity!
        // CAREFUL ABOUT SIGNED ANGLE BETWEEN BEING IN RADIANS!!!
        g_t = Mathf.Cos(Mathf.Deg2Rad * Utilities.GetSignedAngle(redirectionManager.deltaPos, (virtualTargetPosition - realTargetPosition))) * Mathf.Abs(g_t);
        g_r *= Mathf.Sign(redirectionManager.deltaDir);
        // CONSIDER USING SIN NOW FOR ANGLES!

        // Put Caps on Gain Values
        g_t = g_t > 0 ? Mathf.Min(g_t, redirectionManager.globalConfiguration.MAX_TRANS_GAIN) : Mathf.Max(g_t, redirectionManager.globalConfiguration.MIN_TRANS_GAIN);
        g_r = g_r > 0 ? Mathf.Min(g_r, redirectionManager.globalConfiguration.MAX_ROT_GAIN) : Mathf.Max(g_r, redirectionManager.globalConfiguration.MIN_ROT_GAIN);

        // Don't do translation if you're still checking out the previous target
        if ((redirectionManager.currPos - Utilities.UnFlatten(waypoints[waypointIndex - 1])).magnitude < WAYPOINT_UPDATE_DISTANCE)
            g_t = 0;

        //judge nan
        if (float.IsNaN(g_t))
            g_t = 0;
        if (float.IsNaN(g_r))
            g_r = 0;
        if (float.IsNaN(g_c))
            g_c = 0;
        // Translation Gain
        InjectTranslation(g_t * redirectionManager.deltaPos);
        // Rotation Gain
        InjectRotation(g_r * redirectionManager.deltaDir);
        // Curvature Gain
        InjectCurvature(g_c * redirectionManager.deltaPos.magnitude);

    }

    Transform InstantiateDefaultRealTarget(int targetID, Vector3 position)
    {        
        Transform waypoint = new GameObject().transform;
        Destroy(waypoint.GetComponent<SphereCollider>());
        waypoint.parent = transform;
        waypoint.name = "Real Target "+targetID;
        waypoint.position = position;
        waypoint.localScale = 0.3f * Vector3.one;
        return waypoint;
    }

}
