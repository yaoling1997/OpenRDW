using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine.UI;

public class NetworkManager : MonoBehaviourPunCallbacks
{
    private static string roomName = "basic";//room name for networking

    [Tooltip("The maximum number of players per room. When a room is full, it can't be joined by new players, and so new room will be created")]
    [SerializeField]
    private byte maxPlayersPerRoom;    

    private Transform simulatedPlayerHead;//simulated head
    private Transform hmdPlayerHead;//hmd head
    private Transform playerHead;//represent the head of the player
    
    public int avatarId;//avatarId For this player

    private GlobalConfiguration globalConfiguration;
    private RedirectionManager redirectionManager;

    [Tooltip("Prefab used for networking synchronization")]
    public GameObject avatarNetworkingTransformPrefab;
    private GameObject thisAvatarNetworkingTransform;//represent the local avatar, used for networking synchronization
    private Transform realTransform;//the physical transform of the avatar
    private Transform virtualTransform;//the virtual transform of the avatar
    private void Awake()
    {
        globalConfiguration = GetComponentInParent<GlobalConfiguration>();

        //From other networking avatars                
        var redirectedAvatars = globalConfiguration.redirectedAvatars;
        while (redirectedAvatars.Count <= avatarId)
            redirectedAvatars.Add(globalConfiguration.CreateNewRedirectedAvatar(redirectedAvatars.Count));
        globalConfiguration.avatarNum = redirectedAvatars.Count;


        redirectionManager = redirectedAvatars[avatarId].GetComponent<RedirectionManager>();

        hmdPlayerHead = redirectionManager.transform.Find("[CameraRig]").GetComponentInChildren<Camera>(true).transform;

        simulatedPlayerHead = redirectionManager.GetSimulatedAvatarHead();

        if (globalConfiguration.movementController == GlobalConfiguration.MovementController.HMD)
        {
            playerHead = hmdPlayerHead;
        }
        else {
            playerHead = simulatedPlayerHead;
        }        
    }
    // Start is called before the first frame update
    void Start()
    {        
        ConnectToServer();        
    }

    // Update is called once per frame
    void Update()
    {
        if (!globalConfiguration.networkingMode)
            return;
        if (!globalConfiguration.readyToStart)
            return;
        if (playerHead != null) {
            if (thisAvatarNetworkingTransform != null) {
                var spAngle = playerHead.eulerAngles;
                var spPos = playerHead.position;
                virtualTransform.eulerAngles = new Vector3(0, spAngle.y, 0);
                virtualTransform.position = new Vector3(spPos.x, 0, spPos.z);

                realTransform.position = redirectionManager.currPosReal;
                realTransform.forward = redirectionManager.currDirReal;
            }

            var redirectedAvatars = globalConfiguration.redirectedAvatars;

        }        
    }

    private void ConnectToServer()
    {
        PhotonNetwork.NickName = "yaoling1997";
        PhotonNetwork.GameVersion = "1.0";
        PhotonNetwork.ConnectUsingSettings();
        Debug.Log("Try Connect To Server...");
    }

    //Join the room when connected to master
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected To Server");
        base.OnConnectedToMaster();
        RoomOptions roomOptions = new RoomOptions
        {
            MaxPlayers = maxPlayersPerRoom,
            IsVisible = true,
            IsOpen = true
        };

        PhotonNetwork.JoinOrCreateRoom(roomName, roomOptions, TypedLobby.Default);
    }
    public override void OnJoinedRoom()
    {
        Debug.Log("Joined the room");        
        thisAvatarNetworkingTransform = PhotonNetwork.Instantiate(avatarNetworkingTransformPrefab.name, Vector3.zero, Quaternion.identity);
        thisAvatarNetworkingTransform.name = avatarNetworkingTransformPrefab.name + avatarId;
        thisAvatarNetworkingTransform.GetComponent<AvatarInfoForNetworking>().avatarId = avatarId;//synchronize avatarId
        Debug.Log("Create avatarNetworkingTransformPrefab");
        realTransform = thisAvatarNetworkingTransform.transform.Find("Real");
        virtualTransform = thisAvatarNetworkingTransform.transform.Find("Virtual");      

        base.OnJoinedRoom();
    }
    public override void OnLeftRoom()
    {        
        PhotonNetwork.Destroy(thisAvatarNetworkingTransform);
        Debug.Log("OnLeftRoom");
        base.OnLeftRoom();
    }
    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        Debug.Log("A new player joined the room");
        base.OnPlayerEnteredRoom(newPlayer);
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogWarningFormat("PUN Basics Tutorial/Launcher: OnDisconnected() was called by PUN with reason {0}", cause);
        base.OnDisconnected(cause);
    }

    public void ChangeColor(GameObject avatar, Color color)
    {
        var newMaterial = new Material(Shader.Find("Standard"))
        {
            color = color
        };
        foreach (var mr in avatar.GetComponentsInChildren<MeshRenderer>())
        {
            mr.material = newMaterial;
        }
        foreach (var mr in avatar.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            mr.material = newMaterial;
        }
    }

    public void LeaveRoom()
    {
        Debug.Log("Leave Room");
        PhotonNetwork.LeaveRoom();
    }
    private void LateUpdate()
    {
        //Debug.Log("ping: " + PhotonNetwork.GetPing());
    }
}
