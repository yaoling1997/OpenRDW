using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PathSeedChoice = GlobalConfiguration.PathSeedChoice;
using TrackingSpaceChoice = GlobalConfiguration.TrackingSpaceChoice;

public class ExperimentSetup
{
    // store info of every avatar
    public class AvatarInfo {
        public System.Type redirector;
        public System.Type resetter;
        public PathSeedChoice pathSeedChoice;
        public List<Vector2> waypoints;
        public List<float> samplingIntervals;
        public string waypointsFilePath;//waypoints file
        public string samplingIntervalsFilePath;//read sampling intervals from filePath, same line number with waypoints file
        public InitialConfiguration initialConfiguration;
        public AvatarInfo(System.Type redirector, System.Type resetter, PathSeedChoice pathSeedChoice, List<Vector2> waypoints, string waypointsFilePath,
            List<float> samplingIntervals, string samplingIntervalsFilePath, InitialConfiguration initialConfiguration) {
            this.redirector = redirector;
            this.resetter = resetter;
            this.pathSeedChoice = pathSeedChoice;
            this.waypoints = waypoints;
            this.waypointsFilePath = waypointsFilePath;
            this.samplingIntervals = samplingIntervals;
            this.samplingIntervalsFilePath = samplingIntervalsFilePath;
            this.initialConfiguration = initialConfiguration;
        }
        //clone an avatar info
        public AvatarInfo Copy() {
            return new AvatarInfo(redirector, resetter, pathSeedChoice, waypoints, waypointsFilePath, samplingIntervals, samplingIntervalsFilePath, initialConfiguration);
        }
    }
    public List<AvatarInfo> avatars;
    public TrackingSpaceChoice trackingSpaceChoice;
    public List<Vector2> trackingSpacePoints;
    public float squareWidth;
    public List<List<Vector2>> obstaclePolygons;
    public int obstacleType;
    public ExperimentSetup(List<AvatarInfo> avatars, TrackingSpaceChoice trackingSpaceChoice, List<Vector2> trackingSpacePoints, float squareWidth,
        List<List<Vector2>> obstaclePolygons, int obstacleType)
    {
        this.avatars = avatars;
        this.trackingSpaceChoice = trackingSpaceChoice;
        this.trackingSpacePoints = trackingSpacePoints;
        this.squareWidth = squareWidth;
        this.obstaclePolygons = obstaclePolygons;
        this.obstacleType = obstacleType;
    }
}