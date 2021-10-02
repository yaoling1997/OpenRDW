using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class TrackingSpaceGenerator
{
    private const float DEFAULT_TRACKING_SPACE_RADIUS = 5;//default physical tracking space radius
    private const float DEFAULT_TRAPEZOID_W1 = 10;//trapzoid left length
    private const float DEFAULT_TRAPEZOID_W2 = 6;//trapzoid right length
    private const float DEFAULT_TRAPEZOID_H = 10;//trapzoid height
    private const float DEFAULT_CROSS_H = 10;//cross height
    private const float DEFAULT_CROSS_W = 5;//cross width
    private const float DEFAULT_L_SHAPE_W1 = 5;//L_Shape width1
    private const float DEFAULT_L_SHAPE_W2 = 5;//L_Shape width2
    private const float DEFAULT_T_SHAPE_W1 = 4;//T_Shape width1
    private const float DEFAULT_T_SHAPE_W2 = 2;//T_Shape width2
    private const float DEFAULT_T_SHAPE_W3 = 8;//T_Shape width3   
    
    private const float DEFAULT_OBSTACLE_WIDTH = 1;//default square obstacle side length

    private const float TARGET_AREA = 400;

    //generate polygon tracking space with sidenum, center is (0,0)
    public static List<Vector2> GeneratePolygonTrackingSpacePoints(int sideNum, float radius = DEFAULT_TRACKING_SPACE_RADIUS)
    {
        var center = Vector2.zero;        
        var trackingSpacePoints = new List<Vector2>();
        var sampledRotation = 360f / sideNum;//inner angle
        Vector2 startVec;
        
        if (sideNum % 2 == 1)
            startVec = new Vector2(0, radius);
        else
            startVec = new Vector2(radius * Mathf.Sin(sampledRotation / 2 * Mathf.Deg2Rad), radius * Mathf.Cos(sampledRotation / 2 * Mathf.Deg2Rad));
        for (int i = 0; i < sideNum; i++)
        {
            var vec = Utilities.RotateVector(startVec, -sampledRotation * i);//store in counter clock wise
            var pos = center + vec;
            trackingSpacePoints.Add(pos);
        }
        return trackingSpacePoints;
    }
    //generate rectangle tracking space (with obstacle)
    public static void GenerateRectangleTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float width,float height)
    {
        trackingSpacePoints = new List<Vector2> {
            new Vector2(width/2,height/2),
            new Vector2(-width/2,height/2),
            new Vector2(-width/2,-height/2),
            new Vector2(width/2,-height/2)
        };
        obstaclePolygons = new List<List<Vector2>>();

        var distToWall = 2;
        initialConfigurations = new List<InitialConfiguration>();

        foreach (var x in new float[] { -width / 2 + distToWall, width / 2 - distToWall })
            foreach (var y in new float[] { -height / 2 + distToWall, height / 2 - distToWall })
            {
                initialConfigurations.Add(new InitialConfiguration(new Vector2(x, y), new Vector2(-x, -y).normalized));
            }

        switch (obstacleType) {
            case 0:
                break;
            case 1:                
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(3,3),
                    new Vector2(-3,3),
                    new Vector2(-3,-3),
                    new Vector2(3,-3),
                });
                break;
            case 2:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(3,3),
                    new Vector2(5,3),
                    new Vector2(5,5),
                    new Vector2(3,5),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(-3,3),
                    new Vector2(-3,5),
                    new Vector2(-5,5),
                    new Vector2(-5,3),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(-3,-3),
                    new Vector2(-5,-3),
                    new Vector2(-5,-5),
                    new Vector2(-3,-5),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(3,-3),
                    new Vector2(3,-5),
                    new Vector2(5,-5),
                    new Vector2(5,-3),
                });
                initialConfigurations = new List<InitialConfiguration>();
                foreach (var p in new Vector2[] { new Vector2(0, height / 2 - distToWall), new Vector2(0, -height / 2 + distToWall),
                    new Vector2(-width / 2 + distToWall, 0), new Vector2(width / 2 - distToWall, 0) })
                {
                    initialConfigurations.Add(new InitialConfiguration(p, -p.normalized));
                }
                break;
            default:
                break;
        }
    }

    public static void GenerateRectangleTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfiguration)
    {
        GenerateRectangleTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfiguration, Mathf.Sqrt(TARGET_AREA), Mathf.Sqrt(TARGET_AREA));
    }

    //generate triangle tracking space with obstacles
    public static void GenerateTriangleTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float radius)
    {
        trackingSpacePoints = GeneratePolygonTrackingSpacePoints(3, radius);
        obstaclePolygons = new List<List<Vector2>>();
        var rotAngle = 120;

        initialConfigurations = new List<InitialConfiguration>();
        var pList = new Vector2[] {new Vector2(0, radius - 5), new Vector2(0, radius - 8) };
        foreach (var p in pList)
        {
            for (int i = 0; i < 3; i++)
            {
                var newP = Utilities.RotateVector(p, rotAngle * i);
                initialConfigurations.Add(new InitialConfiguration(newP, -newP));

            }
        }
        switch (obstacleType)
        {
            case 0:                
                break;
            case 1:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(3,Mathf.Sqrt(3)),
                    new Vector2(-3,Mathf.Sqrt(3)),
                    new Vector2(0,-2 * Mathf.Sqrt(3)),
                });           
                break;
            case 2:
                var sideWidth = radius * Mathf.Sqrt(3);
                var seg = sideWidth / 6;
                var h = seg * Mathf.Sqrt(3);
                var rectW = 0.5f;//length of small square obstacle
                for (int i = 0; i < 4; i++)
                {
                    var center = new Vector2(-i * seg / 2, h);
                    for (int j = 0; j <= i; j++)
                    {
                        obstaclePolygons.Add(new List<Vector2>{
                            new Vector2(center.x + rectW / 2, center.y + rectW / 2),
                            new Vector2(center.x - rectW / 2, center.y + rectW / 2),
                            new Vector2(center.x - rectW / 2, center.y - rectW / 2),
                            new Vector2(center.x + rectW / 2, center.y - rectW / 2),
                        });
                        center.x += seg;
                    }
                    h -= seg * Mathf.Sqrt(3) / 2;
                }
                
                initialConfigurations = new List<InitialConfiguration>();
                var p2List = new Vector2[] { new Vector2(0, h / 2), new Vector2(-seg, h / 2), new Vector2(seg, h / 2) };                
                foreach (var p in p2List)
                    for (int i = 0; i < 3; i++)
                    {
                        var newP = Utilities.RotateVector(p, rotAngle * i);
                        initialConfigurations.Add(new InitialConfiguration(newP, -newP));
                    }
                break;
            default:
                break;
        }
    }
    public static void GenerateTriangleTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfiguration)
    {
        var r = Mathf.Sqrt(4 * TARGET_AREA / (3 * Mathf.Sqrt(3)));
        GenerateTriangleTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfiguration, r);
        Debug.Log("area:" + ((r + r / 2) * (r / 2 * Mathf.Sqrt(3) * 2) / 2));
    }

    //Trapezoid TrackingSpace points
    public static List<Vector2> GenerateTrapezoidTrackingSpacePoints(float w1 = DEFAULT_TRAPEZOID_W1, float w2 = DEFAULT_TRAPEZOID_W2, float h = DEFAULT_TRAPEZOID_H)
    {
        var trackingSpacePoints = new List<Vector2>
        {
            new Vector2(-h / 2, (w1 + w2) / 4),
            new Vector2(-h / 2, (w1 + w2) / 4 - w1),
            new Vector2(h / 2, (w1 + w2) / 4 - w2),
            new Vector2(h / 2, (w1 + w2) / 4)
        };
        return trackingSpacePoints;
    }
    //Generate Trapezoid TrackingSpace
    public static void GenerateTrapezoidTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float w1, float w2, float height)
    {
        trackingSpacePoints = GenerateTrapezoidTrackingSpacePoints(w1, w2, height);
        obstaclePolygons = new List<List<Vector2>>();
        var halfSide = 2;

        initialConfigurations = new List<InitialConfiguration> {
                    new InitialConfiguration(new Vector2(-height/2+2, 5), Vector2.right),
                    new InitialConfiguration(new Vector2(-height/2+2, -5), Vector2.right),
                    new InitialConfiguration(new Vector2(height/2-2, 7), Vector2.down),
                    new InitialConfiguration(new Vector2(height/2-2, -2), Vector2.up)
                };

        switch (obstacleType)
        {
            case 0:
                break;
            case 1:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(-height/2,1),
                    new Vector2(-height/2,-2),
                    new Vector2(-height/2+10,-2),
                    new Vector2(-height/2+10,1),
                });
                break;
            case 2:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(1,1-2),
                    new Vector2(-1,1-2),
                    new Vector2(-1,-1-2),
                    new Vector2(1,-1-2),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(-height/4+halfSide,(w1+w2)/8+halfSide),
                    new Vector2(-height/4-halfSide,(w1+w2)/8+halfSide),
                    new Vector2(-height/4-halfSide,(w1+w2)/8-halfSide),
                    new Vector2(-height/4+halfSide,(w1+w2)/8-halfSide),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(height/4+halfSide,(w1+w2)/8+halfSide),
                    new Vector2(height/4-halfSide,(w1+w2)/8+halfSide),
                    new Vector2(height/4-halfSide,(w1+w2)/8-halfSide),
                    new Vector2(height/4+halfSide,(w1+w2)/8-halfSide),
                });
                initialConfigurations = new List<InitialConfiguration> {
                    new InitialConfiguration(new Vector2(-height/2+2, 0), Vector2.right),
                    new InitialConfiguration(new Vector2(height/2-2, -1), Vector2.left),
                    new InitialConfiguration(new Vector2(-height/2+2, -6), Vector2.left),
                    new InitialConfiguration(new Vector2(-height/2+2, -3), Vector2.right),
                };
                break;
            default:
                break;
        }
    }
    public static void GenerateTrapezoidTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfiguration)
    {
        var w2 = Mathf.Sqrt(1f / 3 * TARGET_AREA);
        var w1 = 2 * w2;
        var h = w1;
        GenerateTrapezoidTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfiguration, w1, w2, h);
        Debug.Log("area:" + (w1 + w2) / 2 * h);
    }
    //generate cross with center coordinate(0,0), w: center square side length, h: Extension length
    public static List<Vector2> GenerateCrossTrackingSpacePoints(float w = DEFAULT_CROSS_W, float h = DEFAULT_CROSS_H)
    {        
        var trackingSpacePoints = new List<Vector2>();
        var vecs = new Vector2[] { new Vector2(w / 2, w / 2), new Vector2(w / 2, h + w / 2), new Vector2(-w / 2, h + w / 2) };
        var sampledRotation = 90f;
        for (int i = 0; i < 4; i++) {
            foreach (var vec in vecs) {
                var p = Utilities.RotateVector(vec, -sampledRotation * i);
                trackingSpacePoints.Add(p);
            }
        }
        return trackingSpacePoints;
    }
    public static void GenerateCrossTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float w, float h)
    {
        
        trackingSpacePoints = GenerateCrossTrackingSpacePoints(w, h);
        obstaclePolygons = new List<List<Vector2>>();
        var halfSide = 0.25f;
        var halfSide2 = 0.75f;
        

        var p = new Vector2(0, w / 2 + h - 2);
        var rotAngle = 90;
        initialConfigurations = new List<InitialConfiguration>();
        for (int i = 0; i < 4; i++) {
            var newP = Utilities.RotateVector(p, i * rotAngle);
            initialConfigurations.Add(new InitialConfiguration(newP, -newP));
        }
        var width = 4;
        switch (obstacleType)
        {
            case 0:
                break;
            case 1:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(-halfSide,w/2+h-width),
                    new Vector2(-halfSide,-w/2-h+width),
                    new Vector2(halfSide,-w/2-h+width),
                    new Vector2(halfSide,w/2+h-width),
                });
                initialConfigurations = new List<InitialConfiguration>();
                for (int i = 0; i < 4; i++)
                {
                    var newP = Utilities.RotateVector(p, i * rotAngle);
                    initialConfigurations.Add(new InitialConfiguration(newP, -newP));
                }

                initialConfigurations = new List<InitialConfiguration> {
                    new InitialConfiguration(new Vector2(w / 2 + h - 2,0),-new Vector2(w / 2 + h - 1,0)),
                    new InitialConfiguration(new Vector2(w / 2 + h - 5,0),-new Vector2(w / 2 + h - 3,0)),
                    new InitialConfiguration(new Vector2(-w / 2 - h + 2,0),-new Vector2(-w / 2 - h + 1,0)),
                    new InitialConfiguration(new Vector2(-w / 2 - h + 5,0),-new Vector2(-w / 2 - h + 3,0)),
                };
                break;
            case 2:
                var obstaclePos = new List<Vector2> { new Vector2(w / 2 - halfSide2, w / 2 + halfSide2), new Vector2(-w / 2 + halfSide2, w / 2 + halfSide2) };
                for (int i = 0; i < 4; i++)
                    foreach (var op in obstaclePos) {
                        var p2 = Utilities.RotateVector(op, i * 90);
                        obstaclePolygons.Add(new List<Vector2> {
                            new Vector2(p2.x+halfSide2,p2.y+halfSide2),
                            new Vector2(p2.x-halfSide2,p2.y+halfSide2),
                            new Vector2(p2.x-halfSide2,p2.y-halfSide2),
                            new Vector2(p2.x+halfSide2,p2.y-halfSide2),
                        });
                    }
                break;
            default:
                break;
        }
    }
    public static void GenerateCrossTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfiguration)
    {
        var k = 2;
        var w = Mathf.Sqrt(TARGET_AREA / (4 * k + 1));
        var h = k * w;
        
        GenerateCrossTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfiguration, w, h);
    }
    //generate L_shape tracking space, w1: square side length; square center: (0,0); w2 extension length
    public static List<Vector2> GenerateL_ShapeTrackingSpacePoints(float w1 = DEFAULT_L_SHAPE_W1, float w2 = DEFAULT_L_SHAPE_W2)
    {
        var trackingSpacePoints = new List<Vector2>
        {
            new Vector2(w1 / 2, w1 / 2),
            new Vector2(w1 / 2, w1 / 2 + w2),
            new Vector2(-w1 / 2, w1 / 2 + w2),
            new Vector2(-w1 / 2, -w1 / 2),
            new Vector2(w1 / 2 + w2, -w1 / 2),
            new Vector2(w1 / 2 + w2, w1 / 2),
        };
        return trackingSpacePoints;        
    }

    //w1: square side length; w2: extension length
    public static void GenerateL_ShapeTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float w1, float w2)
    {        
        trackingSpacePoints = GenerateL_ShapeTrackingSpacePoints(w1, w2);
        obstaclePolygons = new List<List<Vector2>>();
        var halfSide = 1f;//half length of obstacle
        var dist = 2;
        initialConfigurations = new List<InitialConfiguration> {
            new InitialConfiguration(new Vector2(w1/2+w2-dist,halfSide+dist),Vector2.left),
            new InitialConfiguration(new Vector2(w1/2+w2-dist,-halfSide-dist),Vector2.left),
            new InitialConfiguration(new Vector2(-halfSide-dist,w1/2+w2-dist),Vector2.down),
            new InitialConfiguration(new Vector2(halfSide+dist,w1/2+w2-dist),Vector2.down),
        };

        switch (obstacleType)
        {
            case 0:
                break;
            case 1:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(2,2),
                    new Vector2(-2,2),
                    new Vector2(-2,-2),
                    new Vector2(2,-2),
                });                
                break;
            case 2:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(halfSide,w1/2+1),
                    new Vector2(halfSide,w1/2+w2-3),
                    new Vector2(-halfSide,w1/2+w2-3),
                    new Vector2(-halfSide,w1/2+1),
                });
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(w1/2+1,halfSide),
                    new Vector2(w1/2+1,-halfSide),
                    new Vector2(w1/2+w2-3,-halfSide),
                    new Vector2(w1/2+w2-3,halfSide),
                });
                break;
            default:
                break;
        }
    }
    public static void GenerateL_ShapeTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfiguration)
    {
        var k = 1;
        var w1 =  Mathf.Sqrt(TARGET_AREA / (2 * k + 1));
        var w2 = k * w1;
        Debug.Log("area: " + (w1 * w1 + w1 * w2 * 2));
        GenerateL_ShapeTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfiguration, w1, w2);
    }
    
    public static List<Vector2> GenerateT_ShapeTrackingSpacePoints(float w1 = DEFAULT_T_SHAPE_W1, float w2 = DEFAULT_T_SHAPE_W2, float w3 = DEFAULT_T_SHAPE_W3)
    {
        var trackingSpacePoints = new List<Vector2>
        {
            new Vector2(w1 / 2 + w2, w1 / 2),
            new Vector2(-w1 / 2 - w2, w1 / 2),
            new Vector2(-w1 / 2 - w2, -w1 / 2),
            new Vector2(-w1 / 2, -w1 / 2),
            new Vector2(-w1 / 2, -w1 / 2 - w3),
            new Vector2(w1 / 2, -w1 / 2 - w3),
            new Vector2(w1 / 2, -w1 / 2),
            new Vector2(w1 / 2 + w2, -w1 / 2),
        };
        return trackingSpacePoints;
    }

    //generate T_shape tracking space, w1: square side length, square center: (0,0), w2: short extension length，w3: long extension length
    public static void GenerateT_ShapeTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations, float w1, float w2, float w3)
    {        
        trackingSpacePoints = GenerateT_ShapeTrackingSpacePoints(w1, w2, w3);
        obstaclePolygons = new List<List<Vector2>>();
        var halfSide = 1f;//half length of obstacle

        initialConfigurations = new List<InitialConfiguration> {
            new InitialConfiguration(new Vector2(w1/2+w2-3,0),Vector2.left),
            new InitialConfiguration(new Vector2(-w1/2-w2+3,0),Vector2.right),
            new InitialConfiguration(new Vector2(0,-w1/2-w3+3),Vector2.up),
            new InitialConfiguration(new Vector2(0,-w1/2-w3+6),Vector2.up),
        };

        switch (obstacleType)
        {
            case 0:
                break;
            case 1:
                obstaclePolygons.Add(new List<Vector2> {
                    new Vector2(2,2),
                    new Vector2(-2,2),
                    new Vector2(-2,-2),
                    new Vector2(2,-2),
                });                
                break;
            case 2:
                var obstaclePosList = new List<Vector2> {
                    new Vector2(0,-w1/2-w3+4),
                    new Vector2(-w1/2-w2+4,0),
                    new Vector2(w1/2+w2-4,0),
                };
                foreach (var pos in obstaclePosList) {
                    obstaclePolygons.Add(new List<Vector2> {
                        new Vector2(pos.x+halfSide,pos.y+halfSide),
                        new Vector2(pos.x-halfSide,pos.y+halfSide),
                        new Vector2(pos.x-halfSide,pos.y-halfSide),
                        new Vector2(pos.x+halfSide,pos.y-halfSide),
                    });
                }
                initialConfigurations = new List<InitialConfiguration> {
                    new InitialConfiguration(new Vector2(w1/2+w2-8,0),Vector2.left),
                    new InitialConfiguration(new Vector2(-w1/2-w2+8,0),Vector2.right),
                    new InitialConfiguration(new Vector2(0,-w1/2-w3+7),Vector2.up),
                    new InitialConfiguration(new Vector2(0,-w1/2-w3+10),Vector2.up),
                };
                break;
            default:
                break;
        }
    }
    //generate T_shape tracking space
    public static void GenerateT_ShapeTrackingSpace(int obstacleType, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons, out List<InitialConfiguration> initialConfigurations)
    {
        var k = 3 / 4f;
        var c = 3 / 2f;
        var w1 = Mathf.Sqrt(TARGET_AREA / (1 + 2 * k + c));
        var w2 = k * w1;
        var w3 = c * w1;
        Debug.Log("area:" + (w1 * w1 + w1 * w2 * 2 + w1 * w3));
        GenerateT_ShapeTrackingSpace(obstacleType, out trackingSpacePoints, out obstaclePolygons, out initialConfigurations, w1, w2, w3);
    }

    //generate the mesh of tracking space or obstacle, center is (0,0), enumerate other vertices to generate triangles    
    public static Mesh GeneratePolygonMesh(List<Vector2> polygonPoints) {
        var mesh = new Mesh();
        mesh.hideFlags = HideFlags.DontSave;
        var newVertices = new Vector3[polygonPoints.Count];        
        var newTriangles = new int[(polygonPoints.Count - 2) * 3];
        var newUV = new Vector2[polygonPoints.Count];        

        //use array to replace list
        var pre = new int[polygonPoints.Count];
        var nex = new int[polygonPoints.Count];
        for (int i = 0; i < polygonPoints.Count; i++) {
            newVertices[i] = Utilities.UnFlatten(polygonPoints[i]);
            newUV[i] = Vector2.zero;
            //init list
            pre[i] = (i - 1 + polygonPoints.Count) % polygonPoints.Count;
            nex[i] = (i + 1) % polygonPoints.Count;
        }
        
        int triangleId = 0;
        int pointId = 0;
        int cnt = 0;
        while (triangleId < polygonPoints.Count - 2 && cnt < polygonPoints.Count - triangleId)
        {
            var a = polygonPoints[pre[pointId]];
            var b = polygonPoints[pointId];
            var c = polygonPoints[nex[pointId]];            
            cnt++;
            if (Utilities.Cross(a - b, c - b) <= -1e-6)
            {
                //update triangle
                newTriangles[3 * triangleId] = pointId;
                newTriangles[3 * triangleId + 2] = nex[pointId];
                newTriangles[3 * triangleId + 1] = pre[pointId];
                triangleId++;

                //update list
                nex[pre[pointId]] = nex[pointId];
                pre[nex[pointId]] = pre[pointId];                
                cnt = 0;
            }
            pointId = nex[pointId];                        
        }
        mesh.vertices = newVertices;
        mesh.uv = newUV;
        mesh.triangles = newTriangles;
        //mesh.normals = newnormals;
        mesh.RecalculateNormals();
        return mesh;
    }

    //generate vertial mesh to represent wall
    public static Mesh GenerateWallMesh(List<Vector2> polygonPoints,float height)
    {
        var mesh = new Mesh();
        mesh.hideFlags = HideFlags.DontSave;
        var newVertices = new Vector3[polygonPoints.Count * 2];
        var newTriangles = new int[polygonPoints.Count * 2 * 3];
        var newUV = new Vector2[polygonPoints.Count * 2];

        int triangleId = 0;
        for (int i = 0; i < 2 * polygonPoints.Count; i += 2)
        {
            newVertices[i] = Utilities.UnFlatten(polygonPoints[i / 2]);//原来的点
            newVertices[i + 1] = newVertices[i] + Vector3.down * height;//正下方的点
            newUV[i] = Vector2.zero;
            newUV[i + 1] = Vector2.zero;

            var j = (i + 2) % (2 * polygonPoints.Count);
            newTriangles[3 * triangleId] = i;
            newTriangles[3 * triangleId + 2] = i + 1;
            newTriangles[3 * triangleId + 1] = j;
            triangleId++;

            newTriangles[3 * triangleId] = j;
            newTriangles[3 * triangleId + 2] = i + 1;
            newTriangles[3 * triangleId + 1] = j + 1;
            triangleId++;
        }


        mesh.vertices = newVertices;
        mesh.uv = newUV;
        mesh.triangles = newTriangles;
        //mesh.normals = newnormals;
        mesh.RecalculateNormals();
        return mesh;
    }

    //load TrackingSpace and obstacle Points from file, first description is tracking space, the rest are obstacles
    public static void LoadTrackingSpacePointsFromFile(string path, out List<Vector2> trackingSpacePoints, out List<List<Vector2>> obstaclePolygons)
    {
        if (!File.Exists(path)) {
            Debug.LogError("trackingSpaceFilePath does not exist!");
            trackingSpacePoints = null;
            obstaclePolygons = null;
            return;
        }            
        var reTrackingSpacePoints = new List<Vector2>();
        var reObstaclePolygons = new List<List<Vector2>>();
        var tmpObstaclePolygon = new List<Vector2>();
        bool ifObstacle = false;//if this polygon is obstacle
        try
        {
            var content = File.ReadAllLines(path);
            int lineId = 0;
            foreach (var line in content)
            {
                lineId++;

                if (line.Trim().Length == 0) {
                    
                    if (ifObstacle && tmpObstaclePolygon.Count > 2)
                    {
                        reObstaclePolygons.Add(tmpObstaclePolygon);                        
                    }
                    ifObstacle = true;
                    tmpObstaclePolygon = new List<Vector2>();
                    continue;
                }

                var split = line.Split(',');

                if (split.Length != 2)
                {
                    Debug.LogError("Input TrackingSpacePoints File Error in Line: " + lineId);
                    break;
                }

                var x = float.Parse(split[0]);
                var y = float.Parse(split[1]);
                
                if (!ifObstacle)
                    reTrackingSpacePoints.Add(new Vector2(x, y));
                else
                    tmpObstaclePolygon.Add(new Vector2(x, y));
            }
            if (tmpObstaclePolygon.Count > 2) {
                reObstaclePolygons.Add(tmpObstaclePolygon);
            }
        }
        catch
        {    
            // do nothing
        }
        trackingSpacePoints = reTrackingSpacePoints;
        obstaclePolygons = reObstaclePolygons;
    }
    // add triangle to list in clockwise
    public static void AddTriangle(List<int> newTriangles, int a, int b, int c, bool inner)
    {
        newTriangles.Add(a);
        if (!inner)
        {
            newTriangles.Add(b);
            newTriangles.Add(c);
        }
        else
        {
            newTriangles.Add(c);
            newTriangles.Add(b);
        }
    }

    //generate buffer mesh
    public static Mesh GenerateBufferMesh(List<Vector2> polygonPoints, bool inner, float bufferWidth)
    {
        var mesh = new Mesh();
        mesh.hideFlags = HideFlags.DontSave;
        var newVertices =  new List<Vector3>();
        
        var newTriangles = new List<int>();
        var newUV = new List<Vector2>();        

        float angleSampleRate = 1f;//sampling rate of generating arc buffers

        if (polygonPoints.Count == 0)
            return new Mesh();
        else if (polygonPoints.Count == 1)
        {//draw a circle, like the buffer of an avatar
            var center = polygonPoints[0];
            var initPoint = center + Vector2.up * bufferWidth;
            newVertices.Add(Utilities.UnFlatten(center));
            newVertices.Add(Utilities.UnFlatten(initPoint));
            var vec = initPoint - center;
            for (int i = 1; i < (int)(360f / angleSampleRate); i++) {
                //rotate clockwise
                var nextPoint = Utilities.RotateVector(vec, angleSampleRate * i);
                newVertices.Add(Utilities.UnFlatten(nextPoint));
                //add triangle clockwise
                AddTriangle(newTriangles, 0, newVertices.Count - 2, newVertices.Count - 1, false);
            }
            AddTriangle(newTriangles, 0, newVertices.Count - 1, 1, false);

            foreach (var v in newVertices)
                newUV.Add(Vector2.zero);            
        }
        else
        {//draw the buffer of trackingSpace or obstacles
            //every point has a point set
            var pointSet = new List<Vector2>[polygonPoints.Count];
            var pointSetId = new List<int>[polygonPoints.Count];
            for (int i = 0; i < polygonPoints.Count; i++) {
                var aId = i;
                var bId = (i + 1) % polygonPoints.Count;
                var cId = (i + 2) % polygonPoints.Count;
                var a = polygonPoints[aId];
                var b = polygonPoints[bId];
                var c = polygonPoints[cId];
                pointSet[bId] = new List<Vector2>();

                var rotateDir = inner ? -1 : 1;
                var bv1 = Utilities.RotateVector((b - a).normalized * bufferWidth, rotateDir * 90);
                var bv2 = Utilities.RotateVector((c - b).normalized * bufferWidth, rotateDir * 90);
                var signedAngle = Utilities.GetSignedAngle(bv1, bv2);

                if (Mathf.Approximately(signedAngle, 0))
                {
                    pointSet[bId].Add(b + bv1);
                }
                else if (signedAngle * rotateDir * -1 < 0)
                {
                    var num = (int)(Mathf.Abs(signedAngle) / angleSampleRate);
                    for (int j = 0; j <= num; j++)
                    {
                        var p = b + Utilities.RotateVector(bv1, -j * Mathf.Abs(angleSampleRate));                        
                        pointSet[bId].Add(p);
                    }
                    pointSet[bId].Add(b + bv2);                    
                }
                else
                {
                    var intersection = Utilities.LineLineIntersection(a + bv1, b - a, b + bv2, c - b);
                    pointSet[bId].Add(intersection);                    
                }
            }
            foreach (var p in polygonPoints)
                newVertices.Add(Utilities.UnFlatten(p));

            //mapping between extra point and mesh point
            for (int i = 0; i < pointSet.Length; i++) {
                pointSetId[i] = new List<int>();
                foreach (var p in pointSet[i])
                {
                    newVertices.Add(Utilities.UnFlatten(p));
                    pointSetId[i].Add(newVertices.Count - 1);
                }
            }

            //draw mesh, triangle is in clockwise
            for (int i = 0; i < polygonPoints.Count; i++) {
                for (int j = 0; j < pointSet[i].Count - 1; j++) {
                    AddTriangle(newTriangles, i, pointSetId[i][j + 1], pointSetId[i][j], inner);
                }

                //every edge has two corresponding triangles
                var nextId = (i + 1) % polygonPoints.Count;                
                AddTriangle(newTriangles, i, nextId, pointSetId[nextId][0], inner);
                AddTriangle(newTriangles, i, pointSetId[nextId][0], pointSetId[i][pointSetId[i].Count - 1], inner);
            }

            foreach (var v in newVertices)
                newUV.Add(Vector2.zero);
        }            

        mesh.vertices = newVertices.ToArray();
        mesh.uv = newUV.ToArray();
        mesh.triangles = newTriangles.ToArray();
        //mesh.normals = newnormals;
        mesh.RecalculateNormals();
        return mesh;
    }
    public static void GenerateObstacleMesh(List<List<Vector2>> obstaclePolygons, Transform obstacleParent, Color obstacleColor, bool ifObstacleHasHeight, float obstacleHeight) {
        int obstacleId = 0;
        foreach (var obstaclePoints in obstaclePolygons)
        {
            var obstacleMesh = GeneratePolygonMesh(obstaclePoints);
            var obstacle = new GameObject("obstacleId" + obstacleId);
            obstacle.transform.SetParent(obstacleParent);
            obstacle.transform.localPosition = Vector3.zero;
            obstacle.transform.rotation = Quaternion.identity;

            obstacle.AddComponent<MeshFilter>().mesh = obstacleMesh;
            obstacle.AddComponent<MeshRenderer>().material.color = obstacleColor;
            
            if (ifObstacleHasHeight)
            {
                var wallMesh = TrackingSpaceGenerator.GenerateWallMesh(obstaclePoints, obstacleHeight);
                var wall = new GameObject("wall");
                wall.transform.SetParent(obstacle.transform);
                wall.transform.localPosition = Vector3.zero;
                wall.transform.rotation = Quaternion.identity;

                wall.AddComponent<MeshFilter>().mesh = wallMesh;
                wall.AddComponent<MeshRenderer>().material.color = obstacleColor;
                obstacle.transform.localPosition = Vector3.up * obstacleHeight;//rise obstacle
            }
            obstacleId++;
        }
    }
}
