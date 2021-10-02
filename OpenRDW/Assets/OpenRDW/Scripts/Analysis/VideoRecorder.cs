//originally from https://gist.github.com/DashW/74d726293c0d3aeb53f4
//modified according to needs
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

class BitmapEncoder
{
    public static void WriteBitmap(Stream stream, int width, int height, byte[] imageData)
    {
        using (BinaryWriter bw = new BinaryWriter(stream))
        {

            // define the bitmap file header
            bw.Write((UInt16)0x4D42);                               // bfType;
            bw.Write((UInt32)(14 + 40 + (width * height * 4)));     // bfSize;
            bw.Write((UInt16)0);                                    // bfReserved1;
            bw.Write((UInt16)0);                                    // bfReserved2;
            bw.Write((UInt32)14 + 40);                              // bfOffBits;

            // define the bitmap information header
            bw.Write((UInt32)40);                               // biSize;
            bw.Write((Int32)width);                                 // biWidth;
            bw.Write((Int32)height);                                // biHeight;
            bw.Write((UInt16)1);                                    // biPlanes;
            bw.Write((UInt16)32);                                   // biBitCount;
            bw.Write((UInt32)0);                                    // biCompression;
            bw.Write((UInt32)(width * height * 4));                 // biSizeImage;
            bw.Write((Int32)0);                                     // biXPelsPerMeter;
            bw.Write((Int32)0);                                     // biYPelsPerMeter;
            bw.Write((UInt32)0);                                    // biClrUsed;
            bw.Write((UInt32)0);                                    // biClrImportant;

            // switch the image data from RGB to BGR
            for (int imageIdx = 0; imageIdx < imageData.Length; imageIdx += 3)
            {
                bw.Write(imageData[imageIdx + 2]);
                bw.Write(imageData[imageIdx + 1]);
                bw.Write(imageData[imageIdx + 0]);
                bw.Write((byte)255);
            }

        }
    }

}

/// <summary>
/// Captures frames from a Unity camera in real time
/// and writes them to disk using a background thread.
/// </summary>
/// 
/// <description>
/// Maximises speed and quality by reading-back raw
/// texture data with no conversion and writing 
/// frames in uncompressed BMP format.
/// Created by Richard Copperwaite.
/// </description>
/// 
public class VideoRecorder : MonoBehaviour
{

    // The Encoder Thread
    private Thread encoderThread;

    // Texture Readback Objects    
    private Texture2D tempTexture2D;

    // Timing Data
    private float captureFrameTime;
    private float lastFrameTime;
    private int frameNumber;
    private int savingFrameNumber;
    private int screenWidth;
    private int screenHeight;

    // Encoder Thread Shared Resources
    private Queue<byte[]> frameQueue;
    private string tmpPictureDirectory;
    private string videoDirectory;
    private bool threadIsProcessing;
    private bool terminateThreadWhenDone;

    private StatisticsLogger statisticsLogger;
    private GlobalConfiguration globalConfiguration;
    private string ffmpegPath;

    private int maxFrames;//maximum number of frames you want to record in one video
    private int frameRate;//number of frames to capture per second

    void Start()
    {        
        globalConfiguration= GameObject.Find("OpenRDW").GetComponent<GlobalConfiguration>();
        statisticsLogger = GameObject.Find("OpenRDW").GetComponent<StatisticsLogger>();

        maxFrames = statisticsLogger.maxFrames;
        frameRate = statisticsLogger.frameRate;

        ffmpegPath = Application.dataPath + "/Plugins/ffmpeg.exe";
        // Set target frame rate (optional)
        Application.targetFrameRate = frameRate;

        // Prepare the data directory
        videoDirectory = statisticsLogger.VIDEO_DERECTORY;
        tmpPictureDirectory = statisticsLogger.RESULT_DIRECTORY + "TmpPictures";

        print("Capturing to: " + tmpPictureDirectory + "/");
        if (System.IO.Directory.Exists(tmpPictureDirectory)) {
            Directory.Delete(tmpPictureDirectory, true);
        }

        System.IO.Directory.CreateDirectory(tmpPictureDirectory);
        Debug.Log("Create Directory");
        
        frameQueue = new Queue<byte[]>();

        frameNumber = 0;
        savingFrameNumber = 0;

        captureFrameTime = 1.0f / (float)frameRate;
        lastFrameTime = Time.time;

        // Kill the encoder thread if running from a previous execution
        if (encoderThread != null && (threadIsProcessing || encoderThread.IsAlive))
        {
            threadIsProcessing = false;
            encoderThread.Join();
        }
        if (globalConfiguration.exportVideo) {
            // Start a new encoder thread
            threadIsProcessing = true;
            encoderThread = new Thread(EncodeAndSave);
            encoderThread.Start();
        }
    }

    void OnDisable()
    {
        // Reset target frame rate
        Application.targetFrameRate = -1;

        // Inform thread to terminate when finished processing frames
        terminateThreadWhenDone = true;
    }

    private void LateUpdate()
    {
        if (globalConfiguration.exportVideo && frameNumber <= maxFrames)
        {
            if (tempTexture2D == null) {
                screenWidth= Screen.width;
                screenHeight = Screen.height;
                tempTexture2D = new Texture2D(screenWidth, screenHeight, TextureFormat.RGB24, false);
                Debug.Log(string.Format("init width,height:{0}/{1}", screenWidth, screenHeight));
            }                

            // Calculate number of video frames to produce from this game frame
            // Generate 'padding' frames if desired framerate is higher than actual framerate
            float thisFrameTime = Time.time;
            int framesToCapture = ((int)(thisFrameTime / captureFrameTime)) - ((int)(lastFrameTime / captureFrameTime));

            // Capture the frame
            if (framesToCapture > 0 && Camera.current != null)
            {
                var cam = Camera.current;
                //Debug.Log("cam:" + cam.name, cam.gameObject);
                var oldT = RenderTexture.active;
                var renderTextureTmp = RenderTexture.GetTemporary(screenWidth, screenHeight, 32);
                RenderTexture.active = cam.targetTexture = renderTextureTmp;
                var oldRect = cam.rect;//record screen
                cam.rect = new Rect(0, 0, 1, 1);
                cam.Render();
                //var tmpTexture2D = new Texture2D(cam.targetTexture.width, cam.targetTexture.height);
                tempTexture2D.ReadPixels(new Rect(0, 0, screenWidth, screenHeight), 0, 0);
                RenderTexture.active = oldT;
                cam.targetTexture = null;
                cam.rect = oldRect;
                RenderTexture.ReleaseTemporary(renderTextureTmp);
            }

            // Add the required number of copies to the queue
            for (int i = 0; i < framesToCapture && frameNumber <= maxFrames; ++i)
            {
                frameQueue.Enqueue(tempTexture2D.GetRawTextureData());

                frameNumber++;

                if (frameNumber % frameRate == 0)
                {
                    print("Frame " + frameNumber);
                }
            }

            lastFrameTime = thisFrameTime;

        }
        else //keep making screenshots until it reaches the max frame amount
        {
            // Inform thread to terminate when finished processing frames
            terminateThreadWhenDone = true;

            // Disable script
            this.enabled = false;
        }

    }

    private void EncodeAndSave()
    {
        print("SCREENRECORDER IO THREAD STARTED");

        while (threadIsProcessing)
        {
            if (frameQueue.Count > 0)
            {
                // Generate file path
                string path = tmpPictureDirectory + "/" + savingFrameNumber + ".bmp";

                // Dequeue the frame, encode it as a bitmap, and write it to the file
                using (FileStream fileStream = new FileStream(path, FileMode.Create))
                {
                    BitmapEncoder.WriteBitmap(fileStream, screenWidth, screenHeight, frameQueue.Dequeue());
                    fileStream.Close();
                }

                // Done
                savingFrameNumber++;
                print("Saved " + savingFrameNumber + " frames. " + frameQueue.Count + " frames remaining.");
            }
            else
            {
                if (terminateThreadWhenDone)
                {
                    break;
                }

                Thread.Sleep(1);
            }
        }
        StartConvertion(tmpPictureDirectory, videoDirectory);
        print("tmpPictureDirectory: " + tmpPictureDirectory);
        print("videoDirectory: " + videoDirectory);
        //Directory.Delete(tmpPictureDirectory, true);        
        terminateThreadWhenDone = false;
        threadIsProcessing = false;

        print("SCREENRECORDER IO THREAD FINISHED");        
    }

    //convert pictures to video
    public void StartConvertion(string sourceDir, string targetDir)
    {
        //var command = string.Format("ffmpeg -f image2 -i {0}/%d.jpg {1}/output.mp4", sourceDir, targetDir);
        //var fileName = Application.dataPath + "/Plugins/ffmpeg.exe";
        var fileName = ffmpegPath;        
        var sourceFile = string.Format("\"{0}/%d.bmp\"", sourceDir);
        var targetFile = string.Format("\"{0}/output{1}.mp4\"", targetDir,Utilities.GetTimeStringForFileName());
        //var args = string.Format("-f image2 -i {0} -filter:v fps={1} {2}", sourceFile, frameRate, targetFile);
        var args = string.Format("-framerate {0} -i {1} -c:v libx264 -pix_fmt yuv420p -movflags +faststart {2}", frameRate, sourceFile, targetFile);
        Process p = new Process();
        ProcessStartInfo startInfo = new ProcessStartInfo(fileName, args);
        p.StartInfo.UseShellExecute = false;   //if use Shell
        p.StartInfo.RedirectStandardInput = true;
        p.StartInfo.RedirectStandardOutput = true;
        p.StartInfo.RedirectStandardError = true;
        p.StartInfo.CreateNoWindow = true;        //if show window
        startInfo.WindowStyle = ProcessWindowStyle.Hidden;
        p.StartInfo = startInfo;
        p.Start();
    }
}