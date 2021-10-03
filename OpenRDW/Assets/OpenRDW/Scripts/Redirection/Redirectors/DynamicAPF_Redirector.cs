//Dynamic Artificial Potential Fields for Multi-User Redirected Walking
//https://ieeexplore.ieee.org/abstract/document/9089569

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DynamicAPF_Redirector : APF_Redirector
{
    //The physical space boundary or obstacle boundary is divided into small segments with a growth of no more than targetSegLength long for accumulation
    private static readonly float targetSegLength = 1;

    //Constant parameters used in the formula
    private static readonly float C = 0.00897f;
    private static readonly float lamda = 2.656f;
    private static readonly float gamma = 3.091f;

    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second

    //Unit: degree, the maximum steering value in the proximity based steering rate scaling policy
    private const float M = 15;

    private const float baseRate = 1.5f;//degrees of rotation when the user is standing still
    private const float scaleMultiplier = 2.5f;
    private const float angRateScaleDilate = 1.3f;
    private const float angRateScaleCompress = 0.85f;
    private const float V = 0.1f;//velocity larger than V: use curvature gain; otherwise use rotation gain

    private static readonly float c = 0.25f;
    private static readonly float a1 = 1f;
    private static readonly float a2 = 0.02f;
    private static readonly float b1 = 2f;
    private static readonly float b2 = 1f;

    public override void InjectRedirection()
    {

        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;
        var userTransforms = redirectionManager.globalConfiguration.GetAvatarTransforms();

        //calculate total force by the formulas given by the paper
        var forceT = GetTotalForce(obstaclePolygons, trackingSpacePoints, userTransforms);
        var gravitation = GetGravitationalDir(obstaclePolygons, trackingSpacePoints, userTransforms) * 1 * forceT.magnitude;
        var totalForce = (forceT + gravitation).normalized;

        forceT = forceT.normalized;
        totalForce = totalForce.normalized;

        //update the total force pointer
        UpdateTotalForcePointer(forceT);

        //apply redirection by the calculated force vector
        ApplyRedirectionByForce(totalForce, obstaclePolygons, trackingSpacePoints);

    }

    //calculate the total force by the corresponding paper
    public Vector2 GetTotalForce(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, List<Transform> userTransforms)
    {
        var t = Vector2.zero;
        var w = Vector2.zero;
        var u = Vector2.zero;
        var avatar = Vector2.zero;
        for (int i = 0; i < trackingSpacePoints.Count; i++)
            w += GetW_Force(trackingSpacePoints[i], trackingSpacePoints[(i + 1) % trackingSpacePoints.Count]);
        foreach (var ob in obstaclePolygons)
            for (int i = 0; i < ob.Count; i++)
            {                
                //vertices is in counter-clockwise order, need to swap the positions
                w += GetW_Force(ob[(i + 1) % ob.Count], ob[i]);
            }
        foreach (var user in userTransforms)
            u += GetU_Force(user);
        foreach (var user in userTransforms)
            avatar += GetAvatar_Force(user);

        t = w + u + avatar;        
        return t;
    }
    
    //Obtain the force contributed by each edge of the obstacle and physical space. By default, turn 90°counterclockwise to face trackingspace
    public Vector2 GetW_Force(Vector2 p, Vector2 q)
    {
        var wForce = Vector2.zero;
        
        //divide the long segment to short segments
        var length = (p - q).magnitude;
        var segNum = (int)(length / targetSegLength);
        if (segNum * targetSegLength != length)
            segNum++;
        var segLength = length / segNum;
        
        var unitVec = (q - p).normalized;
        for (int i = 1; i <= segNum; i++)
        {
            var tmpP = p + unitVec * (i - 1) * segLength;
            var tmpQ = p + unitVec * i * segLength;
            wForce += GetW_ForceEverySeg(tmpP, tmpQ);
        }
        return wForce;
    }
    
    //get the force from a segment
    public Vector2 GetW_ForceEverySeg(Vector2 p, Vector2 q)
    {        
        //get center point
        var c = (p + q) / 2;

        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var d = currPos - c;
        
        //normal
        var n = Utilities.RotateVector(q - p, -90).normalized;

        if (Vector2.Dot(n, d.normalized) > 0)
            return C * (q - p).magnitude * d.normalized * 1 / Mathf.Pow(d.magnitude, lamda);
        else
            return Vector2.zero;
    }
    
    //get the force from other user
    public Vector2 GetU_Force(Transform user)
    {        
        //ignore self
        if (user == transform)
            return Vector2.zero;
        var rm = user.GetComponent<RedirectionManager>();
        
        //get real position and direction
        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
        var otherUserDir = Utilities.FlattenedDir2D(rm.currDirReal);
        
        //get local user's position and direction        
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var theta1 = Vector2.Angle(otherUserPos - currPos, currDir);
        var theta2 = Vector2.Angle(currPos - otherUserPos, otherUserDir);
        var k = Mathf.Clamp01((Mathf.Cos(theta1 * Mathf.Deg2Rad) + Mathf.Cos(theta2 * Mathf.Deg2Rad)) / 2);

        var d = currPos - otherUserPos;
        return k * d.normalized * 1 / Mathf.Pow(d.magnitude, gamma);
    }

    public Vector2 GetAvatar_Force(Transform user)
    {
        var rm = user.GetComponent<RedirectionManager>();
                
        if (user == transform)
            return Vector2.zero;

        //get real position and direction of other user
        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
        var otherUserDir = Utilities.FlattenedDir2D(rm.currDirReal);
        
        var avatarPos = otherUserPos + otherUserDir.normalized;
        var avatarDir = otherUserDir;

        //get real position and direction of current user
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        var theta1 = Vector2.Angle(avatarPos - currPos, currDir);
        var theta2 = Vector2.Angle(currPos - avatarPos, avatarDir);
        var k = Mathf.Clamp01((Mathf.Cos(theta1 * Mathf.Deg2Rad) + Mathf.Cos(theta2 * Mathf.Deg2Rad)) / 2);

        var d = currPos - avatarPos;
        return c * k * d.normalized * 1 / Mathf.Pow(d.magnitude, gamma);
    }

    //Get gravitational direction calculated by paper
    public Vector2 GetGravitationalDir(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, List<Transform> userTransforms)
    {
        //get real position and direction of current user
        var currPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);

        //lock potentialArea
        var footPoint1 = Vector2.zero;
        var footPoint2 = Vector2.zero;
        for (int i = 0; i < trackingSpacePoints.Count; i++)
        {
            footPoint1 = Utilities.GetNearestPos(currPos, new List<Vector2> { trackingSpacePoints[i], trackingSpacePoints[(i + 1) % trackingSpacePoints.Count] });
            footPoint2 = Utilities.GetNearestPos(currPos, new List<Vector2> { trackingSpacePoints[(i + 1) % trackingSpacePoints.Count], trackingSpacePoints[(i + 2) % trackingSpacePoints.Count] });
            if (Vector2.Dot(footPoint1 - currPos, currDir) >= 0 && Vector2.Dot(footPoint2 - currPos, currDir) >= 0)
                break;
        }

        //calculate primaryTarget
        var xDir = (footPoint1 - currPos).normalized;
        var yDir = (footPoint2 - currPos).normalized;
        var primaryTarget = currPos;
        float maxsum = 0;
        for (int i = 0; i < (int)(footPoint1 - currPos).magnitude; i++)
        {
            for (int j = 0; j < (int)(footPoint2 - currPos).magnitude; j++)
            {
                var target = currPos + xDir / 2 + yDir / 2 + xDir * i + yDir * j;
                float sum = 0;
                foreach (var user in userTransforms)
                {
                    if (user != transform)
                    {
                        var rm = user.GetComponent<RedirectionManager>();
                        var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
                        sum += (otherUserPos - target).magnitude;
                    }
                }
                if (sum > maxsum)
                {
                    primaryTarget = target;
                    maxsum = sum;
                }
            }
        }


        //lock selectionArea

        List<Vector2> selectPoints = new List<Vector2>();

        for (int i = 0; i < trackingSpacePoints.Count; i++)
            selectPoints.Add(trackingSpacePoints[i]);
        foreach (var user in userTransforms)
        {
            if (user != transform)
            {
                var rm = user.GetComponent<RedirectionManager>();
                var otherUserPos = Utilities.FlattenedPos2D(rm.currPosReal);
                selectPoints.Add(otherUserPos);
            }
        }
        selectPoints.Sort((v1, v2) =>
        {
            return v1[0].CompareTo(v2[0]);
        });


        float maxS = 0;
        float RIGHT = float.PositiveInfinity;
        float LEFT = float.NegativeInfinity;
        float UP = float.PositiveInfinity;
        float DOWN = float.NegativeInfinity;
        for (int left = 0; left < selectPoints.Count - 1; left++)
        {
            for (int right = left + 1; right < selectPoints.Count; right++)
            {
                float leftX = selectPoints[left][0] - primaryTarget[0];
                float rightX = selectPoints[right][0] - primaryTarget[0];
                if (leftX <= 0 && 0 <= rightX)
                {
                    float upY = float.PositiveInfinity;
                    float downY = float.NegativeInfinity;
                    bool findup = false;
                    bool finddown = false;
                    for (int i = left; i <= right; i++)
                    {
                        float dis = selectPoints[i][1] - primaryTarget[1];
                        if (dis >= 0 && dis < upY)
                        {
                            upY = dis;
                            findup = true;
                        }
                        if (dis < 0 && dis > downY)
                        {
                            downY = dis;
                            finddown = true;
                        }
                    }
                    if (findup && finddown && (upY - downY) * (rightX - leftX) >= maxS)
                    {
                        maxS = (upY - downY) * (rightX - leftX);
                        RIGHT = rightX;
                        LEFT = leftX;
                        UP = upY;
                        DOWN = downY;
                    }
                }
            }
        }

        //calculate steeringTarget
        var steeringTarget = primaryTarget;

        xDir = (trackingSpacePoints[1] - trackingSpacePoints[2]).normalized;
        yDir = (trackingSpacePoints[2] - trackingSpacePoints[3]).normalized;
        Vector2 startPoint = primaryTarget + LEFT * xDir + DOWN * yDir;
        maxsum = float.NegativeInfinity;
        for (int i = 0; i < RIGHT - LEFT; i++)
        {
            for (int j = 0; j < UP - DOWN; j++)
            {
                var target = startPoint + xDir / 2 + yDir / 2 + xDir * i + yDir * j;
                float D1 = Utilities.GetNearestDistToObstacleAndTrackingSpace(new List<List<Vector2>>(), trackingSpacePoints, target);
                float D2 = (target - startPoint - xDir * (RIGHT - LEFT) / 2 - yDir * (UP - DOWN) / 2).magnitude;
                float sum = b1 * D1 - b2 * D2;
                if (sum > maxsum)
                {
                    steeringTarget = target;
                    maxsum = sum;
                }
            }
        }

        return (steeringTarget - currPos).normalized;
    }

    //use MessingerAPF for Redirection
    public void ApplyRedirectionByForce(Vector2 force, List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints)
    {
        var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var prevDir = Utilities.FlattenedDir2D(redirectionManager.prevDirReal);
        float g_c = 0;//more rotation calculated by curvature gain
        float g_r = 0;//more rotation calculated by rotation gain        

        //max rotation thresholds
        var deltaTime = redirectionManager.GetDeltaTime();
        var maxRotationFromCurvatureGain = CURVATURE_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;//this rotation no more than
        var maxRotationFromRotationGain = ROTATION_GAIN_CAP_DEGREES_PER_SECOND * deltaTime;

        var desiredFacingDirection = Utilities.UnFlatten(force);//total force vector in physical space
        int desiredSteeringDirection = (-1) * (int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, desiredFacingDirection));

        var generalManager = redirectionManager.globalConfiguration;

        //calculated walking speed
        var v = redirectionManager.deltaPos.magnitude / generalManager.GetDeltaTime();
        float movingRate = 0;

        movingRate = 360 * v / (2 * Mathf.PI * generalManager.CURVATURE_RADIUS);

        //distance to static obstacle
        var distToObstacle = Utilities.GetNearestDistToObstacleAndTrackingSpace(obstaclePolygons, trackingSpacePoints, redirectionManager.currPosReal);

        //distance less than curvature radius, use Proximity-Based Steering Rate Scaling strategy
        if (distToObstacle < generalManager.CURVATURE_RADIUS)
        {
            var h = movingRate;
            var m = distToObstacle;
            var t = 1 - m / generalManager.CURVATURE_RADIUS;
            var appliedSteeringRate = (1 - t) * h + t * M;
            movingRate = appliedSteeringRate;//calculate the steering rate
        }

        g_c = desiredSteeringDirection * Mathf.Min(movingRate * deltaTime, maxRotationFromCurvatureGain);

        var deltaDir = redirectionManager.deltaDir;
        if (deltaDir * desiredSteeringDirection < 0)
        {//rotate away from total force vector
            g_r = desiredSteeringDirection * Mathf.Max(baseRate * deltaTime, Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MIN_ROT_GAIN), maxRotationFromRotationGain));
        }
        else
        {//rotate close total force vector
            g_r = desiredSteeringDirection * Mathf.Max(baseRate * deltaTime, Mathf.Min(Mathf.Abs(deltaDir * redirectionManager.globalConfiguration.MAX_ROT_GAIN), maxRotationFromRotationGain));
        }

        //tracking space rotate clockwise(the avatar rotates counter-clockwise relatively to the tracking space, the avatar remains unchanged relative to the virtual world) 
        //rotate towards negtive gradient        
        if (Mathf.Abs(g_r) > Mathf.Abs(g_c))
        {
            // Rotation Gain
            InjectRotation(g_r);
            g_c = 0;
        }
        else
        {
            // Curvature Gain
            InjectCurvature(g_c);
            g_r = 0;
        }
    }


    //calculate px as priority by the paper    
    public override void GetPriority()
    {
        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;
        var userTransforms = redirectionManager.globalConfiguration.GetAvatarTransforms();

        //calculate the total force needed by the priority        
        var t = GetPriorityForce(obstaclePolygons, trackingSpacePoints, userTransforms);

        //large number means large priority        
        var dir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        redirectionManager.priority = -(a1 * t.magnitude + a2 * Vector2.Angle(t, dir));
    }

    //get total force needed by the priority according to the paper    
    public Vector2 GetPriorityForce(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, List<Transform> userTransforms)
    {
        var t = Vector2.zero;
        var w = Vector2.zero;
        var u = Vector2.zero;

        for (int i = 0; i < trackingSpacePoints.Count; i++)
            w += GetW_Force(trackingSpacePoints[i], trackingSpacePoints[(i + 1) % trackingSpacePoints.Count]);
        foreach (var ob in obstaclePolygons)
            for (int i = 0; i < ob.Count; i++)
            {
                //swap the positions because vertices of the obstacle is in counterclockwise order                
                w += GetW_Force(ob[(i + 1) % ob.Count], ob[i]);
            }
        foreach (var user in userTransforms)
            u += GetU_Force(user);
        
        t = w + u;        
        return t;
    }
}


