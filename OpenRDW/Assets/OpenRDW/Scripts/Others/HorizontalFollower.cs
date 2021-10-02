using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HorizontalFollower : MonoBehaviour
{
    public Transform followedObj;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (followedObj != null)
            transform.position = new Vector3(followedObj.position.x, transform.position.y, followedObj.position.z);
    }
}
