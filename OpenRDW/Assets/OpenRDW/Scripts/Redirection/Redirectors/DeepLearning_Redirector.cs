//Introduction to Barracuda
//https://docs.unity3d.com/Packages/com.unity.barracuda@1.2/manual/index.html

//Supported ONNX operators
//https://docs.unity3d.com/Packages/com.unity.barracuda@1.2/manual/SupportedOperators.html

//A steering algorithm for redirected walking using reinforcement learning
//https://ieeexplore.ieee.org/abstract/document/8998570/
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Barracuda;

public class DeepLearning_Redirector : Redirector
{
    private Model m_runtimeModel;
    private IWorker worker;//used for transfering parameters and execution of barracuda
    private List<Vector3> states;
    private int cnt = 0;//the called time of redrection

    private float []action_meanFloat;//store action_mean values
    private int waitTime = 20;
    public override void InjectRedirection()
    {        
        var obstaclePolygons = redirectionManager.globalConfiguration.obstaclePolygons;
        var trackingSpacePoints = redirectionManager.globalConfiguration.trackingSpacePoints;
        var userTransforms = redirectionManager.globalConfiguration.GetAvatarTransforms();

        //load neural model
        if (m_runtimeModel == null) {
            float boxWidth = 0;
            if (redirectionManager.globalConfiguration.trackingSpaceChoice.Equals(GlobalConfiguration.TrackingSpaceChoice.Square)) {
                boxWidth = redirectionManager.globalConfiguration.squareWidth;
            }
            else
            {
                redirectionManager.globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY);
                boxWidth = (maxX - minX + maxY - minY) / 2;
            }

            var targetW = 10;
            for (int w = 20; w <= 50; w += 10)
            {
                if (Mathf.Abs(targetW - boxWidth) > Mathf.Abs(w - boxWidth))
                {
                    targetW = w;
                }
            }

            //find the most suitable model for loading             
            //Debug.Log("boxWidth: " + boxWidth);
            LoadModel(string.Format("SRLNet_{0}", targetW));
        }
        if (states == null || redirectionManager.ifJustEndReset)
        {
            states = new List<Vector3>();
            for (int i = 0; i < 10; i++)
            {
                AddState();
            }
        }
        cnt++;
        if ((cnt - 1) % waitTime == 0)
        {
            AddState();
            var input = new Tensor(1, 30);
            for (int i = states.Count - 10, j = 0; i < states.Count; i++, j++)
            {
                var state = states[i];
                input[3 * j] = state.x;
                input[3 * j + 1] = state.y;
                input[3 * j + 2] = state.z;                
            }
            worker.Execute(input);            
            var action_mean = worker.PeekOutput("24");
            var value = worker.PeekOutput("output");
            var action_logstd = worker.PeekOutput("38");

            action_meanFloat = new float[action_mean.length];
            for (int i = 0; i < action_meanFloat.Length; i++)
                action_meanFloat[i] = action_mean[i];

            //release cache
            input.Dispose();
            action_mean.Dispose();
            value.Dispose();
            action_logstd.Dispose();
        }

        var g_t = action_meanFloat[0];
        var g_r = action_meanFloat[1];
        var g_c = action_meanFloat[2];

        var gm = redirectionManager.globalConfiguration;
        g_t = Convert(-1, 1, gm.MIN_TRANS_GAIN, gm.MAX_TRANS_GAIN, g_t);
        g_r = Convert(-1, 1, gm.MIN_ROT_GAIN, gm.MAX_ROT_GAIN, g_r);        
        g_c = Convert(-1, 1, -1 / gm.CURVATURE_RADIUS, 1 / gm.CURVATURE_RADIUS, g_c);

        var translation = g_t * redirectionManager.deltaPos;
        var rotation = g_r * redirectionManager.deltaDir;
        var curvature = g_c * redirectionManager.deltaPos.magnitude * Mathf.Rad2Deg;
        
        // Translation Gain
        InjectTranslation(translation);
        InjectRotation(rotation);
        InjectCurvature(curvature);
    }
    public void AddState() {
        //get the max and min vertices of the trackingspace bounding box
        redirectionManager.globalConfiguration.GetTrackingSpaceBoundingbox(out float minX, out float maxX, out float minY, out float maxY);        
        var pos = Utilities.FlattenedPos2D(redirectionManager.currPosReal);
        var dir = Utilities.FlattenedDir2D(redirectionManager.currDirReal);
        var xTmp = Convert(minX, maxX, 0, 1, pos.x);
        var yTmp = Convert(minY, maxY, 0, 1, pos.y);
        var zTmp = Convert(-180, 180, 0, 1, Vector2.SignedAngle(Vector2.right, dir));//direction
        states.Add(new Vector3(xTmp, yTmp, zTmp));
    }
    //load model by name
    public void LoadModel(string modelName) {        
        m_runtimeModel = ModelLoader.Load((NNModel)Resources.Load(modelName));
        worker = WorkerFactory.CreateWorker(m_runtimeModel, WorkerFactory.Device.GPU);//run on GPU in default
    }
    /// <summary>
    /// Convert v from range(l1,r1) to range(l2,r2)
    /// </summary>
    public float Convert(float l1,float r1,float l2,float r2,float v) {
        return (v - l1) / (r1 - l1) * (r2 - l2) + l2;
    }
}