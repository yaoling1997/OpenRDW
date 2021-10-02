using UnityEngine;
using System.Collections;


public class KeyboardController : MonoBehaviour {

    private GlobalConfiguration globalConfiguration;
    private RedirectionManager redirectionManager;
    private MovementManager movementManager;    
    private NetworkManager networkManager;

    [Tooltip("Auto-Adjust automatically counters curvature as human naturally would.")]
    [SerializeField]
    bool useAutoAdjust = true;

    float lastCurvatureApplied = 0;
    float lastRotationApplied = 0;    

    private void Awake()
    {
        redirectionManager = GetComponentInParent<RedirectionManager>();
        movementManager = GetComponentInParent<MovementManager>();
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        networkManager = globalConfiguration.GetComponentInChildren<NetworkManager>(true);
    }

    public void MakeOneStepKeyboardMovement() {        
        if (!globalConfiguration.avatarIsWalking
            || globalConfiguration.movementController != GlobalConfiguration.MovementController.Keyboard
            || globalConfiguration.currentShownAvatarId != movementManager.avatarId
            || (globalConfiguration.networkingMode && networkManager.avatarId != movementManager.avatarId))
            return;
        
        Vector3 userForward = Utilities.FlattenedDir3D(transform.forward);
        Vector3 userRight = Utilities.FlattenedDir3D(transform.right);
        var deltaTime = redirectionManager.GetDeltaTime();

        var ts = globalConfiguration.translationSpeed;
        var rs = globalConfiguration.rotationSpeed;

        if (Input.GetKey(KeyCode.W))
        {
            transform.Translate(ts * deltaTime * userForward, Space.World);
        }
        if (Input.GetKey(KeyCode.S))
        {
            transform.Translate(-ts * deltaTime * userForward, Space.World);
        }
        if (Input.GetKey(KeyCode.D))
        {
            transform.Translate(ts * deltaTime * userRight, Space.World);
        }
        if (Input.GetKey(KeyCode.A))
        {
            transform.Translate(-ts * deltaTime * userRight, Space.World);
        }

        if (Input.GetKey(KeyCode.UpArrow))
        {
            transform.Rotate(userRight, -rs * deltaTime, Space.World);
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            transform.Rotate(userRight, rs * deltaTime, Space.World);
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            transform.Rotate(Vector3.up, rs * deltaTime, Space.World);
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            transform.Rotate(Vector3.up, -rs * deltaTime, Space.World);
        }


        if (useAutoAdjust)
        {
            transform.Rotate(Vector3.up, -lastCurvatureApplied, Space.World);
            lastCurvatureApplied = 0; // We set it to zero meaning we applied what was last placed. This prevents constant application of rotation when curvature isn't applied.

            transform.Rotate(Vector3.up, -lastRotationApplied, Space.World);
            lastRotationApplied = 0; // We set it to zero meaning we applied what was last placed. This prevents constant application of rotation when rotation isn't applied.

        }
    }

    public void SetLastCurvature(float rotationInDegrees)
    {
        lastCurvatureApplied = rotationInDegrees;
    }

    public void SetLastRotation(float rotationInDegrees)
    {
        lastRotationApplied = rotationInDegrees;
    }
}
