using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VisibilityPolygonCSharp;


/* 伪代码 */
/* polyPhys = computeVisPoly(envPhys, curPosPhys, curDirPhys) // Polygon由中心点pos和各个顶点vertices组成
 * polyVirt = computeVisPoly(envVirt, curPosVirt, curDirVirt) // curDir为在环境中的当前朝向
 * sliceVirt = getActiveSlice(polyVirt, curDir) // 根据每片三角形的角平分线，选择离当前朝向最近的三角形
 * slicePhys = getMostSimilarSlice(polyPhys, sliceVirt)
 * g_t, g_c, g_r = getGains(slicePhys, curDirPhys)
 */
/* 接口需求 */
/* getPhysObstacle 取得物理环境的障碍物（边界也算），用顶点序列逆时针排列表示
 * getVirtObstacle 取得虚拟环境的障碍物（边界也算）
 * getPhysPos 取得物理位置坐标
 * getVirtPos 取得虚拟环境中的位置坐标
 * getPhysDir 取得物理空间中的当前朝向
 * getVirtDir 取得虚拟空间中的当前朝向
 * 
 */
public class VisPoly_Redirector : APF_Redirector
{
    private const float CURVATURE_GAIN_CAP_DEGREES_PER_SECOND = 15;  // degrees per second
    private const float ROTATION_GAIN_CAP_DEGREES_PER_SECOND = 30;  // degrees per second
    private bool sliceVisible = false; // set selected virtual slice and physical slice visible
    private VisibilityPolygonCSharp<Vector2> VisibilityPolygon = new VisibilityPolygonCSharp<Vector2>(new Vector2Adapter());

    public List<List<Vector2>> getVirtObstacle()
    {
        List<List<Vector2>> res = new List<List<Vector2>>();
        MeshFilter[] meshfilters = globalConfiguration.virtualWorld.GetComponentsInChildren<MeshFilter>();
        if (meshfilters != null)
        {
            foreach (MeshFilter meshfilter in meshfilters)
            {
                Mesh mesh = meshfilter.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                List<Vector2> tmp = new List<Vector2>();
                for (var i = 0; i + 2 < triangles.Length; i++)
                {
                    tmp.Add(Utilities.FlattenedPos2D(meshfilter.transform.TransformPoint(vertices[triangles[i]])));
                    tmp.Add(Utilities.FlattenedPos2D(meshfilter.transform.TransformPoint(vertices[triangles[i+1]])));
                    tmp.Add(Utilities.FlattenedPos2D(meshfilter.transform.TransformPoint(vertices[triangles[i+2]])));
                }
                res.Add(tmp);
                //Debug.Log(meshfilter.sharedMesh);
                //Debug.Log(string.Format("triangle: {0} {1} {2}", tmp[0], tmp[1], tmp[2]));
            }
        }
        return res;
    }

    public List<Vector2> computeVisPoly(List<List<Vector2>> obstacle, Vector2 pos, out List<Vector2> sb)
    {
        List<Vector2> res = new List<Vector2>();
        sb = new List<Vector2>();
        //
        IList<IList<Vector2>> realObstacle = new List<IList<Vector2>>();
        obstacle.ForEach(i => realObstacle.Add(i));
        List<Segment<Vector2>> segments = VisibilityPolygonCSharp<Vector2>.ConvertToSegments(realObstacle);
        
        //segments = VisibilityPolygon.BreakIntersections(segments);
        //return res;
        res = VisibilityPolygon.Compute(pos, segments);
        float sumArea = 0.0f;
        //Debug.Log(string.Format("vispolygon vertices count: {0}" ,res.Count));
        for (var i = 0; i < res.Count; i++)
        {
            //Debug.Log(string.Format("slices: {0} {1} {2}", pos, res[i], res[(i + 1) % res.Count]));
            Vector2 tmp = ((res[i]-pos).normalized + (res[(i + 1) % res.Count]-pos).normalized).normalized;
            tmp = tmp * Mathf.Abs(Utilities.Cross(res[i] - pos, res[(i + 1) % res.Count] - pos));
            sb.Add(tmp);
            sumArea += Mathf.Abs(Utilities.Cross(res[i] - pos, res[(i + 1) % res.Count] - pos));
        }
        for (var i = 0; i < sb.Count; i++)
        {
            sb[i] = sb[i] / sumArea;
        }
        return res;
    }

    public List<Vector2> getActiveSlice(List<Vector2> poly, Vector2 pos, Vector2 dir, List<Vector2> sb, out int index)
    {
        List<Vector2> res = new List<Vector2>();
        //return res;
        res.Add(pos);
        float minAngle = 180;
        index = 0;
        Vector2 minSeg1 = poly[0];
        Vector2 minSeg2 = poly[1%poly.Count];
        for (var i = 0; i < poly.Count; i++)
        {
            float tmp = Vector2.Angle(sb[i], dir);
            if (tmp < minAngle)
            {
                index = i;
                minAngle = tmp;
                minSeg1 = poly[i];
                minSeg2 = poly[(i + 1) % poly.Count];
            }
        }
        res.Add(minSeg1);
        res.Add(minSeg2);
        return res;
    }
    public override void InjectRedirection()
    {
        //List<List<Vector2>> physObstacle = new List<List<Vector2>>();
        //globalConfiguration.obstaclePolygons.ForEach(i => physObstacle.Add(i));
        List<List<Vector2>> physObstacle = globalConfiguration.obstaclePolygons;
        physObstacle.Add(globalConfiguration.trackingSpacePoints);
        var virtObstacle = getVirtObstacle();
        var physPos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var physDir = Utilities.FlattenedPos2D(redirectionManager.currDirReal);
        var virtPos = Utilities.FlattenedPos2D(redirectionManager.currPos);
        var virtDir = Utilities.FlattenedPos2D(redirectionManager.currDir);

        List<Vector2> physPoly = computeVisPoly(physObstacle, physPos, out List<Vector2> physSb);
        List<Vector2> virtPoly = computeVisPoly(virtObstacle, virtPos, out List<Vector2> virtSb);

        List<Vector2> sliceVirt = getActiveSlice(virtPoly, virtPos, virtDir, virtSb, out int index); // clockwise default
        if (sliceVisible)
        {
            Mesh mesh = GetComponent<MeshFilter>().mesh;
            Vector3[] vertices = new Vector3[]
            {
                redirectionManager.GetPosReal(Utilities.UnFlatten(sliceVirt[0], 2f)),
                redirectionManager.GetPosReal(Utilities.UnFlatten(sliceVirt[1], 2f)),
                redirectionManager.GetPosReal(Utilities.UnFlatten(sliceVirt[2], 2f)),
            };
            int[] triangles = new int[] { 0, 1, 2 };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            //mesh.RecalculateNormals();
        }
        getNegativeGradient(virtSb[index].magnitude, physSb, out Vector2 ng);
        ApplyRedirectionByNegativeGradient(ng);

        /* Debug.Log(string.Format("maze: {0}", virtualEnv.transform.Find("Maze").gameObject)); */

    }
    public void getNegativeGradient(float target, List<Vector2> sb, out Vector2 ng)
    {
        float min_s = Mathf.Abs(target - sb[0].magnitude);
        int index = 0;
        for (var i = 0; i < sb.Count; i++)
        {
            float tmp = Mathf.Abs(target - sb[i].magnitude);
            if (min_s > tmp)
            {
                min_s = tmp;
                index = i;
            }
        }

        ng = Vector2.zero;
        ng = ng + sb[index];

        ng = ng.normalized;
        UpdateTotalForcePointer(ng);
    }
    public void ApplyRedirectionByNegativeGradient(Vector2 ng)
    {
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
        else
        {
            // Curvature Gain
            InjectCurvature(g_c);
            g_r = 0;
        }
    }
}
