using UnityEngine;
using System.Collections;
using System.Collections.Generic;


//align to the vector calculated by artificial potential fileds, rotate to the side of the larger angle
public class APF_Resetter : Resetter {

    float requiredRotateSteerAngle = 0;//steering angle，rotate the physical plane and avatar together

    float requiredRotateAngle = 0;//normal rotation angle, only rotate avatar

    float rotateDir;//rotation direction, positive if rotate clockwise

    float speedRatio;

    APF_Redirector redirector;

    public override bool IsResetRequired()
    {
        return IfCollisionHappens();
    }
        
    public override void InitializeReset()
    {        
        var redirectorTmp = redirectionManager.redirector;        

        if (redirectorTmp.GetType().IsSubclassOf(typeof(APF_Redirector)))
        {
            redirector = (APF_Redirector)redirectorTmp;
            var totalForce = redirector.totalForce;
            var currDir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
            var targetRealRotation = 360 - Vector2.Angle(totalForce, currDir);//required rotation angle in real world
            
            rotateDir = -(int)Mathf.Sign(Utilities.GetSignedAngle(redirectionManager.currDirReal, Utilities.UnFlatten(totalForce)));
            
            requiredRotateSteerAngle = 360 - targetRealRotation;
            
            requiredRotateAngle = targetRealRotation;

            speedRatio = requiredRotateSteerAngle / requiredRotateAngle;
            
            SetHUD((int)rotateDir);
        }
        else {
            Debug.Log("RedirectorType: " + redirectorTmp.GetType());            
            Debug.LogError("non-APF redirector can't use APF_resetter");
        }            
    }

    public override void InjectResetting()
    {        
        var steerRotation = speedRatio * redirectionManager.deltaDir;            
        if (Mathf.Abs(requiredRotateSteerAngle) <= Mathf.Abs(steerRotation) || requiredRotateAngle == 0)
        {//meet the rotation requirement
            InjectRotation(requiredRotateSteerAngle);

            //reset end
            redirectionManager.OnResetEnd();
            requiredRotateSteerAngle = 0;

        }
        else
        {//rotate the rotation calculated by ratio
            InjectRotation(steerRotation);
            requiredRotateSteerAngle -= Mathf.Abs(steerRotation);            
        }
    }
    
    public override void EndReset()
    {
        DestroyHUD();
    }

    public override void SimulatedWalkerUpdate()
    {
        // Act is if there's some dummy target a meter away from you requiring you to rotate        
        var rotateAngle = redirectionManager.GetDeltaTime() * redirectionManager.globalConfiguration.rotationSpeed;
        //finish specified rotation
        if (rotateAngle >= requiredRotateAngle)
        {
            rotateAngle = requiredRotateAngle;
            //Avoid accuracy error
            requiredRotateAngle = 0;
        }
        else {
            requiredRotateAngle -= rotateAngle;
        }
        redirectionManager.simulatedWalker.RotateInPlace(rotateAngle * rotateDir);
    }

}
