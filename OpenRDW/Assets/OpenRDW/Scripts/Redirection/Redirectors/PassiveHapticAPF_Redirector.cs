//A general reactive algorithm for redirected walking using artificial potential functions
//http://www.jeraldthomas.com/files/thomas2019general.pdf

using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public class PassiveHapticAPF_Redirector : APF_Redirector
{
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    //parameters used by the paper
    private const float Aa = 1;
    private const float Ba = 2;
    private const float Ao = 1;
    private const float Bo = -1;
    private bool alignmentState = false;//alignmentState == true: only use attractive force，alignmentState == false: only use repulsive force
    
    public override void InjectRedirection()
    {
        var obstaclePolygons = globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = globalConfiguration.trackingSpacePoints;
        
        GetRepulsiveOrAttractiveForceAndNegativeGradient(obstaclePolygons, trackingSpacePoints, out float rf, out Vector2 ng);
        ApplyRedirectionByNegativeGradient(ng);
    }

    //calculate attractive force or repulsive force and negative gradient
    public void GetRepulsiveOrAttractiveForceAndNegativeGradient(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, out float rf, out Vector2 ng) {
        var nearestPosList = new List<Vector2>();
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);

        //contribution from borders
        for (int i = 0; i < trackingSpacePoints.Count; i++) {
            var p = trackingSpacePoints[i];
            var q = trackingSpacePoints[(i + 1) % trackingSpacePoints.Count];
            var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });
            //Debug.Log(p.ToString() + q.ToString() + nearestPos.ToString());
            nearestPosList.Add(nearestPos);
        }

        //contribution from obstacles
        foreach (var obstacle in obstaclePolygons) {
            var nearestPos = Utilities.GetNearestPos(currPosReal, obstacle);
            nearestPosList.Add(nearestPos);
        }

        //contribution from other avatars
        foreach (var avatar in globalConfiguration.redirectedAvatars) {
            var avatarId = avatar.GetComponent<MovementManager>().avatarId;
            //ignore self
            if (avatarId == movementManager.avatarId)
                continue;
            var nearestPos = avatar.GetComponent<RedirectionManager>().currPosReal;
            nearestPosList.Add(Utilities.FlattenedPos2D(nearestPos));
        }
        IfChangeAlignmentState();
        rf = 0;
        ng = Vector2.zero;
        
        ng = AttractiveNegtiveGradient(currPosReal) + ObstacleNegtiveGradient(currPosReal, nearestPosList);

        ng = ng.normalized;
        UpdateTotalForcePointer(ng);
    }
    private Vector2 AttractiveNegtiveGradient(Vector2 currPosReal) {        
        var physicalTargetPosReal = globalConfiguration.physicalTargetTransforms[movementManager.avatarId].position;
        var gDelta = 2 * (new Vector2(currPosReal.x - physicalTargetPosReal.x, currPosReal.y - physicalTargetPosReal.y));
        return -gDelta;//NegtiveGradient
    }
    private Vector2 ObstacleNegtiveGradient(Vector2 currPosReal, List<Vector2> nearestPosList) {
        var ng = Vector2.zero;
        float rf = 0;//totalforce
        foreach (var obPos in nearestPosList)
        {
            rf += 1 / (currPosReal - obPos).magnitude;

            //get contribution from each obstacle
            var gDelta = -Ao * Mathf.Pow(Mathf.Pow(currPosReal.x - obPos.x, 2) + Mathf.Pow(currPosReal.y - obPos.y, 2), -3f / 2) * (currPosReal - obPos);            
            ng += -gDelta;//NegtiveGradient
        }
        return ng;
    }
    
    public void IfChangeAlignmentState(){
        if (alignmentState)
            return;

        //position and direction in physical tracking space
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDirReal = Utilities.FlattenedDir2D(redirectionManager.currDirReal);        
        var gc = globalConfiguration;
        var objVirtualPos = movementManager.waypoints[movementManager.waypoints.Count - 1];
        var objPhysicalPos = gc.physicalTargetTransforms[movementManager.avatarId].position;

        //the virtual distance from the user to the alignment target
        var Dv = (objVirtualPos - Utilities.FlattenedPos2D(redirectionManager.currPos)).magnitude;

        //the physical distance from the user to the alignment target
        var Dp = (objPhysicalPos - currPosReal).magnitude;

        var gt = gc.MIN_TRANS_GAIN + 1;
        var Gt = gc.MAX_TRANS_GAIN + 1;
        //the physical rotational oﬀset
        var phiP = Vector2.Angle(currDirReal, objPhysicalPos - currPosReal) * Mathf.Deg2Rad;
        if (gt * Dp < Dv && Dv < Gt * Dp) {
            if (phiP < Mathf.Asin((Dp * 1 / gc.CURVATURE_RADIUS) / 2))
            {
                alignmentState = true;
                Debug.Log("alignmentState = true");
            }
        }
    }
    
    public void ApplyRedirectionByNegativeGradient(Vector2 ng) {
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var prevDir = Utilities.FlattenedDir2D(redirectionManager.prevDirReal);
        float g_c = 0;
        float g_r = 0;
        float g_t = 0;

        //calculate translation
        if (Vector2.Dot(ng, currDir) < 0)
        {
            g_t = -globalConfiguration.MIN_TRANS_GAIN;
        }
        
        var deltaTime = redirectionManager.GetDeltaTime();
        var maxRotationFromCurvatureGain = CURVATURE_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;
        var maxRotationFromRotationGain = ROTATION_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;

        var desiredFacingDirection = Utilities.UnFlatten(ng);//negative gradient direction in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));

        //calculate curvature rotation
        var rotationFromCurvatureGain = Mathf.Rad2Deg * (redirectionManager.deltaPos.magnitude / redirectionManager.globalConfiguration.CURVATURE_RADIUS);

        g_c = desiredSteeringDirection * Mathf.Min(rotationFromCurvatureGain, maxRotationFromCurvatureGain);

        var deltaDir = redirectionManager.deltaDir;
        if (deltaDir * desiredSteeringDirection < 0)
        {
            g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain);
        }
        else
        {
            g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain);
        }

        // Translation Gain
        InjectTranslation(g_t * redirectionManager.deltaPos);

        if (Mathf.Abs(g_r) > Mathf.Abs(g_c))
        {
            // Rotation Gain
            InjectRotation(g_r);
            g_c = 0;
        }
        else {
            // Curvature Gain
            InjectCurvature(g_c);
            g_r = 0;
        }
    }
}