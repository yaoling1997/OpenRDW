using UnityEngine;
using System.Collections;

public class HeadFollower : MonoBehaviour {        
    private RedirectionManager redirectionManager;
    private MovementManager movementManager;

    [HideInInspector]
    public bool ifVisible;

    [HideInInspector]
    public GameObject avatar;//avatar for visualization

    [HideInInspector]
    public GameObject avatarRoot;//avatar root, control movement like translation and rotation, Avoid interference of action data

    private Vector3 prePos;
    private Animator animator;

    private GlobalConfiguration globalConfiguration;

    [HideInInspector]
    public int avatarId;

    private bool hasCreatedAvatar;//if already create the avatar visualization

    private void Awake()
    {
        redirectionManager = GetComponentInParent<RedirectionManager>();
        movementManager = GetComponentInParent<MovementManager>();
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();
        ifVisible = true;        
    }

    public void CreateAvatarViualization() {
        if (hasCreatedAvatar)
            return;
        hasCreatedAvatar = true;
        avatarId = movementManager.avatarId;
        avatarRoot = globalConfiguration.CreateAvatar(transform, movementManager.avatarId);
        animator = avatarRoot.GetComponentInChildren<Animator>();
        avatar = animator.gameObject;
    }

    // Use this for initialization
    void Start () {
        prePos = transform.position;
    }
	
    public void UpdateManually() {
        transform.position = redirectionManager.currPos;        
        if (redirectionManager.currDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(redirectionManager.currDir, Vector3.up);

        prePos = transform.position;
    }    

    //change the color of the avatar
    public void ChangeColor(Color color) {
        var newMaterial= new Material(Shader.Find("Standard"));
        newMaterial.color = color;
        foreach (var mr in avatar.GetComponentsInChildren<MeshRenderer>())
        {
            mr.material = newMaterial;            
        }
        foreach (var mr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            mr.material = newMaterial;
        }
    }
    public void SetAvatarBodyVisibility(bool ifVisible) {
        foreach (var mr in GetComponentsInChildren<MeshRenderer>())
            mr.enabled = ifVisible;
        foreach (var sr in GetComponentsInChildren<SkinnedMeshRenderer>())
            sr.enabled = ifVisible;
    }
}
