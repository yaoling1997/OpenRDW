using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public abstract class Resetter : MonoBehaviour {
    private static float toleranceAngleError = 1;//Allowable angular error to prevent jamming
    [HideInInspector]
    public RedirectionManager redirectionManager;

    [HideInInspector]
    public MovementManager simulationManager;

    //spin in place hint
    public Transform prefabHUD = null;

    public Transform instanceHUD;

    private void Awake()
    {
        simulationManager = GetComponent<MovementManager>();        
    }

    /// <summary>
    /// Function called when reset trigger is signalled, to see if resetter believes resetting is necessary.
    /// </summary>
    /// <returns></returns>
    public abstract bool IsResetRequired();

    public abstract void InitializeReset();

    public abstract void InjectResetting();

    public abstract void EndReset();

    //manipulation when update every reset
    public abstract void SimulatedWalkerUpdate();


    //rotate physical plane clockwise
    public void InjectRotation(float rotationInDegrees)
    {
        transform.RotateAround(Utilities.FlattenedPos3D(redirectionManager.headTransform.position), Vector3.up, rotationInDegrees);        
        GetComponentInChildren<KeyboardController>().SetLastRotation(rotationInDegrees);        
    }

    public void Initialize()
    {

    }

    public float GetDistanceToCenter()
    {
        return redirectionManager.currPosReal.magnitude;
    }
    
    public bool IfCollisionHappens()
    {
        var realPos = new Vector2(redirectionManager.currPosReal.x, redirectionManager.currPosReal.z);
        var realDir = new Vector2(redirectionManager.currDirReal.x, redirectionManager.currDirReal.z);
        var polygons = new List<List<Vector2>>();
        var trackingSpacePoints = simulationManager.generalManager.trackingSpacePoints;
        var obstaclePolygons = simulationManager.generalManager.obstaclePolygons;
        var userGameobjects = simulationManager.generalManager.redirectedAvatars;
        
        //collect polygons for collision checking
        polygons.Add(trackingSpacePoints);
        foreach (var obstaclePolygon in obstaclePolygons)
            polygons.Add(obstaclePolygon);

        var ifCollisionHappens = false;
        foreach (var polygon in polygons)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                var p = polygon[i];
                var q = polygon[(i + 1) % polygon.Count];
                
                //judge vertices of ploygons
                if (IfCollideWithPoint(realPos, realDir, p))
                {
                    ifCollisionHappens = true;
                    //Debug.Log("point reset true");
                    break;
                }
                
                //judge edge collision
                if (Vector3.Cross(q - p, realPos - p).magnitude / (q - p).magnitude <= redirectionManager.globalConfiguration.RESET_TRIGGER_BUFFER//distance
                    && Vector2.Dot(q - p, realPos - p) >= 0 && Vector2.Dot(p - q, realPos - q) >= 0//range
                    )
                {                    
                    //if collide with border
                    if (Mathf.Abs(Cross(q - p, realDir)) > 1e-3 && Mathf.Sign(Cross(q - p, realDir)) != Mathf.Sign(Cross(q - p, realPos - p)))
                    {
                        ifCollisionHappens = true;
                        break;
                    }
                }
            }
            if (ifCollisionHappens)
                break;
        }        
        
        if (!ifCollisionHappens)
        {//if collide with other avatars
            foreach (var us in userGameobjects)
            {                
                //ignore self
                if (us.Equals(gameObject))
                    continue;
                //collide with other avatars
                if (IfCollideWithPoint(realPos, realDir, Utilities.FlattenedPos2D(us.GetComponent<RedirectionManager>().currPosReal)))
                {
                    ifCollisionHappens = true;                    
                    break;
                }
            }
        }

        return ifCollisionHappens;
    }

    //if collide with vertices
    public bool IfCollideWithPoint(Vector2 realPos, Vector2 realDir, Vector2 obstaclePoint)
    {
        //judge point, if the avatar will walks into a circle obstacle
        var pointAngle = Vector2.Angle(obstaclePoint - realPos, realDir);
        return (obstaclePoint - realPos).magnitude <= redirectionManager.globalConfiguration.RESET_TRIGGER_BUFFER && pointAngle < 90 - toleranceAngleError;
    }
    private float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    
    //initialize spin in place hint, rotateDir==1:rotate clockwise, otherwise, rotate counter clockwise
    public void SetHUD(int rotateDir)
    {
        if (prefabHUD == null)
            prefabHUD = Resources.Load<Transform>("Resetter HUD");
        
        if (simulationManager.ifVisible) {
            instanceHUD = Instantiate(prefabHUD);
            instanceHUD.parent = redirectionManager.headTransform;
            instanceHUD.localPosition = instanceHUD.position;
            instanceHUD.localRotation = instanceHUD.rotation;

            //rotate clockwise
            if (rotateDir == 1)
            {
                instanceHUD.GetComponent<TextMesh>().text = "Spin in Place\n→";
            }
            else
            {
                instanceHUD.GetComponent<TextMesh>().text = "Spin in Place\n←";
            }
        }
    }

    //destroy HUD object
    public void DestroyHUD() {
        if (instanceHUD != null)
            Destroy(instanceHUD.gameObject);
    }
}
