using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class APF_Redirector : Redirector
{
    public Vector2 totalForce;//vector calculated by artificial potential fields(total force or negtive gradient), can be used by apf-resetting
    public GameObject totalForcePointer;//visualization of totalForce

    public void UpdateTotalForcePointer(Vector2 forceT)
    {
        //record this new force
        totalForce = forceT;
        var simulationManager = redirectionManager.movementManager;

        if (totalForcePointer == null && !redirectionManager.globalConfiguration.runInBackstage)
        {
            totalForcePointer = Instantiate(redirectionManager.globalConfiguration.negArrow);
            totalForcePointer.transform.SetParent(transform);
            totalForcePointer.transform.position = Vector3.zero;
            foreach (var mr in totalForcePointer.GetComponentsInChildren<MeshRenderer>())
            {
                mr.enabled = redirectionManager.movementManager.ifVisible;
            }
            //Debug.Log("generate arrows, ifVisible: "+ redirectionManager.simulationManager.ifVisible);
            //Debug.Log("UserId: " + redirectionManager.simulationManager.userId);
        }

        if (totalForcePointer != null)
        {
            totalForcePointer.SetActive(simulationManager.ifVisible);
            totalForcePointer.transform.position = redirectionManager.currPos;

            if (forceT.magnitude > 0)
                totalForcePointer.transform.forward = transform.rotation * Utilities.UnFlatten(forceT);
        }
    }

    private void OnDestroy()
    {
        if (totalForcePointer != null)
            Destroy(totalForcePointer);
    }
}
