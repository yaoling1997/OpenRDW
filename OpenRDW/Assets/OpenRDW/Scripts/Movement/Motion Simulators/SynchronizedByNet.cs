using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SynchronizedByNet : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void UpdateTransform(Transform virtualTransform, Transform realTransform) {
        var redirectedAvatar = transform.GetComponentInParent<RedirectionManager>().transform;
        redirectedAvatar.rotation = virtualTransform.rotation * Quaternion.Inverse(realTransform.rotation);
        redirectedAvatar.position = virtualTransform.position - redirectedAvatar.rotation * realTransform.position;

        transform.position = virtualTransform.position;
        transform.forward = virtualTransform.forward;
    }
}
