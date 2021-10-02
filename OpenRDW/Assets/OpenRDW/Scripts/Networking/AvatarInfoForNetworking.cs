using Photon.Pun;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//record avatar info for network synchronization
public class AvatarInfoForNetworking : MonoBehaviourPunCallbacks, IPunObservable
{
    public int avatarId;//avatarId for synchronization
    private GlobalConfiguration globalConfiguration = null;
    private Transform realT;
    private Transform virtualT;
    private SynchronizedByNet synchronizedByNet;
    private bool receiveData = false;
    private void Awake()
    {
        realT = transform.Find("Real");
        virtualT = transform.Find("Virtual");
        Debug.Log("AvatarInfoForNetworking awake");
        DontDestroyOnLoad(gameObject);
    }
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {        
        //We own this avatar, send data
        if (stream.IsWriting)
        {
            stream.SendNext(this.avatarId);
            //Debug.Log("Send avatarId:" + avatarId);
        }
        else
        {//network avatar, receive data
            this.avatarId = (int)stream.ReceiveNext();
            //Debug.Log("receive avatarId:" + avatarId);
            receiveData = true;
        }
    }
    private void LateUpdate()
    {
        var pv = GetComponent<PhotonView>();

        //other player and received avatarId
        if (receiveData)
        {
            if (globalConfiguration == null)
            {
                globalConfiguration = GameObject.Find("OpenRDW").GetComponent<GlobalConfiguration>();       
                var redirectedAvatars = globalConfiguration.redirectedAvatars;
                while (redirectedAvatars.Count <= avatarId)
                    redirectedAvatars.Add(globalConfiguration.CreateNewRedirectedAvatar(redirectedAvatars.Count));
                globalConfiguration.avatarNum = redirectedAvatars.Count;
            }
            if (synchronizedByNet == null)
            {
                synchronizedByNet = globalConfiguration.redirectedAvatars[avatarId].transform.Find("Simulated Avatar").Find("Head").GetComponent<SynchronizedByNet>();
            }
            synchronizedByNet.UpdateTransform(virtualT, realT);
            //Debug.Log(string.Format("receive data, avatarid:{0}, time:{1}", avatarId, Time.time));
        }
    }
}
