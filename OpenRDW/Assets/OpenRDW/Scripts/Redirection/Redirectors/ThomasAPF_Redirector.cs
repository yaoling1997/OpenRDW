//A general reactive algorithm for redirected walking using artificial potential functions
//http://www.jeraldthomas.com/files/thomas2019general.pdf

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class ThomasAPF_Redirector : APF_Redirector
{
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second
    
    public override void InjectRedirection()
    {
        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;

        //get repulsive force and negative gradient
        GetRepulsiveForceAndNegativeGradient(obstaclePolygons, trackingSpacePoints, out float rf, out Vector2 ng);
        ApplyRedirectionByNegativeGradient(ng);
    }
    
    public void GetRepulsiveForceAndNegativeGradient(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, out float rf, out Vector2 ng) {
        var nearestPosList = new List<Vector2>();
        var currPosReal = Utilities.FlattenedPos2D(redirectionManager.currPosReal);

        //physical borders' contributions
        for (int i = 0; i < trackingSpacePoints.Count; i++) {
            var p = trackingSpacePoints[i];
            var q = trackingSpacePoints[(i + 1) % trackingSpacePoints.Count];
            var nearestPos = Utilities.GetNearestPos(currPosReal, new List<Vector2> { p, q });            
            nearestPosList.Add(nearestPos);
        }

        //obstacle contribution
        foreach (var obstacle in obstaclePolygons) {
            var nearestPos = Utilities.GetNearestPos(currPosReal, obstacle);
            nearestPosList.Add(nearestPos);
        }

        //consider avatar as point obstacles
        foreach (var user in redirectionManager.globalConfiguration.redirectedAvatars) {
            var uId = user.GetComponent<MovementManager>().avatarId;
            //ignore self
            if (uId == redirectionManager.movementManager.avatarId)
                continue;
            var nearestPos = user.GetComponent<RedirectionManager>().currPosReal;
            nearestPosList.Add(Utilities.FlattenedPos2D(nearestPos));
        }

        rf = 0;
        ng = Vector2.zero;
        foreach (var obPos in nearestPosList) {
            rf += 1 / (currPosReal - obPos).magnitude;

            //get gradient contributions
            var gDelta = -Mathf.Pow(Mathf.Pow(currPosReal.x - obPos.x, 2) + Mathf.Pow(currPosReal.y - obPos.y, 2), -3f / 2) * (currPosReal - obPos);
            
            ng += -gDelta;//negtive gradient
        }
        ng = ng.normalized;
        UpdateTotalForcePointer(ng);
    }

    //apply redirection by negtive gradient
    public void ApplyRedirectionByNegativeGradient(Vector2 ng) {
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var prevDir = Utilities.FlattenedDir2D(redirectionManager.prevDirReal);
        float g_c = 0;//curvature
        float g_r = 0;//rotation
        float g_t = 0;//translation

        //calculate translation
        if (Vector2.Dot(ng, currDir) < 0)
        {
            g_t = -redirectionManager.globalConfiguration.MIN_TRANS_GAIN;
        }
        
        var deltaTime = redirectionManager.GetDeltaTime();
        var maxRotationFromCurvatureGain = CURVATURE_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;
        var maxRotationFromRotationGain = ROTATION_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;

        var desiredFacingDirection = Utilities.UnFlatten(ng);//vector of negtive gradient in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));

        //calculate rotation by curvature gain
        var rotationFromCurvatureGain = Mathf.Rad2Deg * (redirectionManager.deltaPos.magnitude / redirectionManager.globalConfiguration.CURVATURE_RADIUS);

        g_c = desiredSteeringDirection * Mathf.Min(rotationFromCurvatureGain, maxRotationFromCurvatureGain);

        var deltaDir = redirectionManager.deltaDir;
        if (deltaDir * desiredSteeringDirection < 0)
        {//rotate away from negtive gradient
            g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain);
        }
        else
        {//rotate towards negtive gradient
            g_r = desiredSteeringDirection * Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain);
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