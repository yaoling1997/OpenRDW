using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.IO;

public class UserInterfaceManager : MonoBehaviour
{
    [Tooltip("The canvas which shows hints")]
    public Canvas canvasOverlay;
    private GameObject panelExperimentComplete;
    [HideInInspector]
    public List<string> commandFiles;

    private GlobalConfiguration globalConfiguration;
    private void Awake()
    {
        globalConfiguration = GetComponent<GlobalConfiguration>();
        panelExperimentComplete = canvasOverlay.transform.Find("PanelExperimentComplete").gameObject;
    }
    private void Start()
    {

    }
    
    public void GetCommandFilePaths() {        
        commandFiles = new List<string>();

        if (!globalConfiguration.multiCmdFiles)
        {
            if (Utilities.GetCommandFilePath(out string path))
            {
                //load single command file
                commandFiles.Add(path);
            }
        }
        else
        {
            if (Utilities.GetCommandDirPath(out string path))
            {
                //load multiple command files from this directory
                commandFiles = Utilities.GetCommandFilesRecursively(path);
            }
        }
    }
    public void SetActivePanelExperimentComplete(bool active) {
        panelExperimentComplete.SetActive(active);
    }
}
