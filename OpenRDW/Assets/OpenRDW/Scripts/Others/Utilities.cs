using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Utilities
{
    public static readonly float eps = 1e-5f;

    public static Vector3 FlattenedPos3D(Vector3 vec, float height = 0)
    {
        return new Vector3(vec.x, height, vec.z);
    }

    public static Vector2 FlattenedPos2D(Vector3 vec)
    {
        return new Vector2(vec.x, vec.z);
    }

    public static Vector3 FlattenedDir3D(Vector3 vec)
    {
        return (new Vector3(vec.x, 0, vec.z)).normalized;
    }

    public static Vector2 FlattenedDir2D(Vector3 vec)
    {
        return new Vector2(vec.x, vec.z).normalized;
    }

    public static Vector3 UnFlatten(Vector2 vec, float height = 0)
    {
        return new Vector3(vec.x, height, vec.y);
    }

    /// <summary>
    /// Gets angle from prevDir to currDir in degrees, assuming the vectors lie in the xz plane (with left handed coordinate system).
    /// positive means rotate clockwise
    /// </summary>
    public static float GetSignedAngle(Vector3 prevDir, Vector3 currDir)
    {
        return Mathf.Sign(Vector3.Cross(prevDir, currDir).y) * Vector3.Angle(prevDir, currDir);
    }

    public static Vector3 GetRelativePosition(Vector3 pos, Transform origin)
    {
        return Quaternion.Inverse(origin.rotation) * (pos - origin.position);
    }

    public static Vector3 GetRelativeDirection(Vector3 dir, Transform origin)
    {
        return Quaternion.Inverse(origin.rotation) * dir;
    }
    public static float Cross(Vector2 a, Vector2 b) {
        return a.x * b.y - a.y * b.x;
    }

    // Based on: http://stackoverflow.com/questions/4780119/2d-euclidean-vector-rotations
    // FORCED LEFT HAND ROTATION AND DEGREES
    //rotate clockwise        
    public static Vector2 RotateVector(Vector2 fromOrientation, float thetaInDegrees)
    {
        Vector2 ret = Vector2.zero;
        float cos = Mathf.Cos(-thetaInDegrees * Mathf.Deg2Rad);
        float sin = Mathf.Sin(-thetaInDegrees * Mathf.Deg2Rad);
        ret.x = fromOrientation.x * cos - fromOrientation.y * sin;
        ret.y = fromOrientation.x * sin + fromOrientation.y * cos;
        return ret;
    }

    public static bool Approximately(Vector2 v0, Vector2 v1)
    {
        return Mathf.Approximately(v0.x, v1.x) && Mathf.Approximately(v0.y, v1.y);
    }

    //Get intersection of two lines
    //refer to https://stackoverflow.com/questions/59449628/check-when-two-vector3-lines-intersect-unity3d
    public static Vector2 LineLineIntersection(Vector2 p,Vector2 v,Vector2 q,Vector2 w)
    {
        var u = p - q;
        var t = Cross(w, u) / Cross(v, w);
        return p + t * v;
    }

    /// <summary>
    /// get command files recirsively, may be slow when it is deep
    /// </summary>
    public static List<string> GetCommandFilesRecursively(string currentDir) {
        var files = new List<string>(Directory.GetFiles(currentDir));

        var dirs = Directory.GetDirectories(currentDir);
        foreach (var dir in dirs) {
            var subFiles = GetCommandFilesRecursively(dir);
            foreach (var subFile in subFiles)
                files.Add(subFile);
        }
        return files;
    }

    public static bool GetCommandDirPath(out string path)
    {
        
        #if UNITY_EDITOR
            path = EditorUtility.OpenFolderPanel("Choose the dir of command files", "", "");
        #endif
        return path.Length > 0;
    }

    //choose file path in windows
    public static bool GetCommandFilePath(out string path)
    {
        path = "";
        OpenFileName ofn = new OpenFileName();
        ofn.structSize = Marshal.SizeOf(ofn);
        ofn.filter = "Command File(*.txt)\0*.txt";
        ofn.file = new string(new char[256]);
        ofn.maxFile = ofn.file.Length;
        ofn.fileTitle = new string(new char[64]);
        ofn.maxFileTitle = ofn.fileTitle.Length;
        ofn.initialDir = Application.streamingAssetsPath.Replace('/', '\\');//default path
        ofn.title = "Import Command File";
        ofn.flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008;
        if (LocalDialog.GetOpenFileName(ofn))
        {
            if (File.Exists(ofn.file))
            {
                path = ofn.file;
                return true;
            }
            else
            {
                Debug.Log("No Such Command File!");
                return false;
            }
        }
        return false;
    }
    public static void ExportTexture2dToPng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
    }
    //physical coordinate to pixel coordinate
    public static Vector2 RealPosToPixelPos(Texture2D tex, Vector2 p, float sideLength) {
        return (p + new Vector2(sideLength / 2, sideLength / 2)) / sideLength * tex.width;
    }

    // a,b: positions in real tracking space, sideLength: side length of the square image, center coordinate is (0,0)        
    //draw a line from a to b，type indicates how to draw the line, 0:no points, 1: draw b point, 2: draw a,b points
    public static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float sideLength, int thickness, Color colorBegin, Color colorEnd, int type)
    {
        //change to pixel coordinate
        var p = RealPosToPixelPos(tex, a, sideLength);
        var q = RealPosToPixelPos(tex, b, sideLength);

        float width = q.x - p.x;
        float height = q.y - p.y;
        var length = Mathf.Max(Mathf.Abs(width), Mathf.Abs(height));
        int intLength = Mathf.RoundToInt(length);
        var dx = width == 0 ? 0 : width / length;
        var dy = height == 0 ? 0 : height / length;
        var dColor = (colorEnd - colorBegin) / intLength;
        for (int i = 0; i < intLength; i++)
        {
            var x = Mathf.RoundToInt(p.x);
            var y = Mathf.RoundToInt(p.y);

            if (IfPosInTex(tex, x, y))
                SetPixelWithThickness(tex, x, y, thickness, colorBegin + i * dColor);
            p += new Vector2(dx, dy);
        }

        var pointColor = new Color(0.3843138f, 0.5333334f, 0.8039216f);
        var m = 4f;
        var pointThickness = Mathf.RoundToInt(thickness * m);
        if (type == 1)
        {
            DrawPoint(tex, b, sideLength, pointThickness, pointColor);
        }
        else if (type == 2)
        {
            DrawPoint(tex, a, sideLength, pointThickness, pointColor);
            DrawPoint(tex, b, sideLength, pointThickness, pointColor);
        }
    }
    public static void DrawPoint(Texture2D tex, Vector2 p, float sideLength, int thickness, Color c) {
        p = RealPosToPixelPos(tex, p, sideLength);
        SetPixelWithThickness(tex, Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y), thickness, c);
    }
                
    public static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float sideLength, int thickness, Color colorBegin, Color colorEnd)
    {
        DrawLine(tex, a, b, sideLength, thickness, colorBegin, colorEnd, 0);
    }
    public static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float sideLength, int thickness, Color color) {
        DrawLine(tex, a, b, sideLength, thickness, color, color);
    }
    public static void DrawLine(Texture2D tex, Vector2 a, Vector2 b, float sideLength, int thickness, Color color,int type)
    {
        DrawLine(tex, a, b, sideLength, thickness, color, color, type);
    }
    public static void SetTextureToSingleColor(Texture2D t,Color c) {
        for (int i = 0; i < t.width; i++)
            for (int j = 0; j < t.height; j++)
                t.SetPixel(i, j, c);
        t.Apply();
    }
    public static void DrawPolygon(Texture2D tex, List<Vector2> polygonPoints, float sideLength, int thickness, Color color) {
        var pixelPolygonPoints = new List<Vector2>();
        var minX = float.MaxValue;
        var maxX = float.MinValue;
        var minY = float.MaxValue;
        var maxY = float.MinValue;
        foreach (var p in polygonPoints) {
            var pixelPos = RealPosToPixelPos(tex, p, sideLength);
            pixelPolygonPoints.Add(pixelPos);
            minX = Mathf.Min(minX, pixelPos.x);
            maxX = Mathf.Max(maxX, pixelPos.x);
            minY = Mathf.Min(minY, pixelPos.y);
            maxY = Mathf.Max(maxY, pixelPos.y);
        }

        for (int i = (int)minX; i <= maxX; i++)
            for (int j = (int)minY; j <= maxY; j++)
            {
                if (IfPosInTex(tex, i, j) && IfPosInPolygon(pixelPolygonPoints, new Vector2(i, j)))
                {
                    //Debug.Log(string.Format("i:{0},j:{1},color:{2}", i, j, color));
                    SetPixelWithThickness(tex, i, j, thickness, color);
                }
            }
    }

    //if p in polygon
    public static bool IfPosInPolygon(List<Vector2> polygonPoints, Vector2 p) {
        //rotate the polygon, then judge by the ray
        float rotation = 321.543f;//avoid special cases
        //float rotation = 0f;
        var polygonPointsTmp = new List<Vector2>(polygonPoints);
        var pTmp = RotateVector(p, rotation);
        for (int i = 0; i < polygonPointsTmp.Count; i++) {
            polygonPointsTmp[i] = RotateVector(polygonPointsTmp[i], rotation);
        }
        //enumerate edge, record the number of intersections, odd indicates it is inside
        int cnt = 0;
        for (int i = 0; i < polygonPointsTmp.Count; i++) {
            var a = polygonPointsTmp[i];
            var b = polygonPointsTmp[(i + 1) % polygonPointsTmp.Count];
            var k = Dcmp(Cross(b - a, pTmp - a));
            var d1 = Dcmp(a.y - pTmp.y);
            var d2 = Dcmp(b.y - pTmp.y);

            if (IfPosOnSeg(pTmp, a, b))
                return true;
            if (k > 0 && d1 <= 0 && d2 > 0)
                cnt++;
            if (k < 0 && d2 <= 0 && d1 > 0)
                cnt--;
        }
            
        return cnt % 2 != 0;
    }
    public static bool IfPosOnSeg(Vector2 a, Vector2 p, Vector2 q) {
        if (Dcmp(Mathf.Abs(Cross(q - p, a - p))) == 0 && Dcmp(Vector2.Dot(p - a, q - a)) <= 0)
        {
            return true;
        }
        return false;
    }

    public static bool IfPosInTex(Texture2D tex, int x, int y)
    {
        return 0 <= x && x < tex.width && 0 <= y && y < tex.height;
    }
    public static void SetPixelWithThickness(Texture2D tex, int x, int y,int thickRadius,Color color)
    {
        for (int i = x - thickRadius + 1; i <= x + thickRadius - 1; i++)
            for (int j = y - thickRadius + 1; j <= y + thickRadius - 1; j++) {                    
                if (IfPosInTex(tex, i, j) && Dcmp((new Vector2(i, j) - new Vector2(x, y)).magnitude - thickRadius) <= 0)
                {
                    tex.SetPixel(i, j, color);
                }
            }
    }
    public static int Dcmp(float x) {
        if (Mathf.Abs(x) < eps)
            return 0;
        return x < 0 ? -1 : 1;
    }

    //Gaussian blur
    public static Texture2D BlurTexEdge(Texture2D oldT,Color backgroundColor) {            
        var t = new Texture2D(oldT.width,oldT.height);
        t.filterMode = oldT.filterMode;
        int blurWidth = 2;//blur radius
        for (int i = 0; i < t.width; i++)
            for (int j = 0; j < t.height; j++) {
                var c = Color.clear;
                if (oldT.GetPixel(i, j).Equals(backgroundColor))
                {
                    c = backgroundColor;                        
                }
                else {
                    int cnt = 0;
                    for (int k = i - blurWidth + 1; k <= i + blurWidth - 1; k++)
                        for (int z = j - blurWidth + 1; z <= j + blurWidth - 1; z++)
                            if (IfPosInTex(t, k, z))
                            {
                                c += oldT.GetPixel(k, z);
                                cnt++;
                            }
                    c /= cnt;
                }
                t.SetPixel(i, j, c);
            }
        t.Apply();
        return t;
    }

    //Get Nearest point of the obstacle to the pos, obstacle: vertices of a static obstacle
    public static Vector2 GetNearestPos(Vector2 pos, List<Vector2> obstacle)
    {
        float minDist = float.MaxValue;//record min dist
        var rePos = Vector2.zero;
        for (int i = 0; i < obstacle.Count; i++)
        {
            var p = obstacle[i];
            var q = obstacle[(i + 1) % obstacle.Count];

            //foot of a perpendicular is on the segment
            if (Vector2.Dot(q - p, pos - p) > 0 && Vector2.Dot(p - q, pos - q) > 0)
            {
                var perp = p + (q - p).normalized * Vector2.Dot(q - p, pos - p) / (q - p).magnitude;//foot of a perpendicular

                var dist = (pos - perp).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    rePos = perp;
                }
            }
            else//foot of a perpendicular outside the segment, only caculate the distance between vertices
            {
                var dist = (pos - p).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    rePos = p;
                }
                dist = (pos - q).magnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    rePos = q;
                }
            }
        }
        return rePos;
    }

    //Get nearest distance between currPosReal and obstacles/boundaries
    public static float GetNearestDistToObstacleAndTrackingSpace(List<List<Vector2>> obstaclePolygons, List<Vector2> trackingSpacePoints, Vector2 currPosReal)
    {
        var currPos = FlattenedPos2D(currPosReal);
        var nearestPosList = new List<Vector2>();
        for (int i = 0; i < trackingSpacePoints.Count; i++)
        {
            var p = trackingSpacePoints[i];
            var q = trackingSpacePoints[(i + 1) % trackingSpacePoints.Count];
            var nearestPos = GetNearestPos(currPos, new List<Vector2> { p, q });                
            nearestPosList.Add(nearestPos);
        }

        foreach (var obstacle in obstaclePolygons)
        {
            var nearestPos = GetNearestPos(currPos, obstacle);
            nearestPosList.Add(nearestPos);
        }

        //Get min distance
        float re = float.MaxValue;
        foreach (var p in nearestPosList)
        {
            re = Mathf.Min((currPos - p).magnitude, re);
        }
        return re;
    }
    public static string GetTimeString()
    {
        return System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
    }
    public static string GetTimeStringForFileName() {
        return GetTimeString().Replace(':', '-');
    }

    // The Path will already have "/" at the end
    public static string GetProjectPath()
    {
        #if UNITY_EDITOR
                return Application.dataPath.Substring(0, Application.dataPath.Length - 7) + "/";
        #else
                    return Application.dataPath;
        #endif
    }

    public static void CreateDirectoryIfNeeded(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    public static void CaptureScreenShot(string path, int superSize = 1)
    {
        Debug.Log("CaptureScreenShot, save to " + path);
        ScreenCapture.CaptureScreenshot(path, superSize);
    }
}    