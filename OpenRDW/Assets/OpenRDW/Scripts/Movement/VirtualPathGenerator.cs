using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class VirtualPathGenerator
{

    public static int RANDOM_SEED = 3041;
    
    public static Vector2 defaultStartPoint = Vector2.zero;

    public enum DistributionType { Normal, Uniform };
    public enum AlternationType { None, Random, Constant };

    static float zigLength = 5f;
    static float zagAngle = 140;
    static int zigzagWaypointCount = 40;

    public struct SamplingDistribution
    {
        public DistributionType distributionType;
        public float min, max;
        public float mu, sigma;
        public AlternationType alternationType; // Used typicaly for the case of generating angles, where we want the value to be negated at random
        public SamplingDistribution(DistributionType distributionType, float min, float max, AlternationType alternationType = AlternationType.None, float mu = 0, float sigma = 0)
        {
            this.distributionType = distributionType;
            this.min = min;
            this.max = max;
            this.mu = mu;
            this.sigma = sigma;
            this.alternationType = alternationType;
        }
    }

    public struct PathSeed
    {
        public int waypointCount;
        public SamplingDistribution distanceDistribution;
        public SamplingDistribution angleDistribution;
        public PathSeed(SamplingDistribution distanceDistribution, SamplingDistribution angleDistribution, int waypointCount)
        {
            this.distanceDistribution = distanceDistribution;
            this.angleDistribution = angleDistribution;
            this.waypointCount = waypointCount;
        }
        public static PathSeed GetPathSeed90Turn()
        {
            SamplingDistribution distanceSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, 2, 8);
            SamplingDistribution angleSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, 90, 90, AlternationType.Random);
            int waypointCount = 40;
            return new PathSeed(distanceSamplingDistribution, angleSamplingDistribution, waypointCount);
        }

        public static PathSeed GetPathSeedSawtooth()
        {
            SamplingDistribution distanceSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, zigLength, zigLength);
            SamplingDistribution angleSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, zagAngle, zagAngle, AlternationType.Constant);
            int waypointCount = zigzagWaypointCount;
            return new PathSeed(distanceSamplingDistribution, angleSamplingDistribution, waypointCount);
        }

        public static PathSeed GetPathSeedRandomTurn()
        {            
            SamplingDistribution distanceSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, 2, 8);

            SamplingDistribution angleSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, -180, 180);
            int waypointCount = 50;
            return new PathSeed(distanceSamplingDistribution, angleSamplingDistribution, waypointCount);
        }

        public static PathSeed GetPathSeedStraightLine()
        {
            SamplingDistribution distanceSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, 20, 20);            
            SamplingDistribution angleSamplingDistribution = new SamplingDistribution(DistributionType.Uniform, 0, 0);
            int waypointCount = 10;
            return new PathSeed(distanceSamplingDistribution, angleSamplingDistribution, waypointCount);
        }
    }

    static float SampleUniform(float min, float max)
    {        
        return Random.Range(min, max);
    }

    static float SampleNormal(float mu = 0, float sigma = 1, float min = float.MinValue, float max = float.MaxValue)
    {
        // From: http://stackoverflow.com/questions/218060/random-gaussian-variables
        float r1 = Random.value;
        float r2 = Random.value;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(r1)) * Mathf.Sin(2.0f * Mathf.PI * r2); // Random Normal(0, 1)
        float randNormal = mu + randStdNormal * sigma;
        return Mathf.Max(Mathf.Min(randNormal, max), min);
    }

    static float SampleDistribution(SamplingDistribution distribution)
    {
        float retVal = 0;
        if (distribution.distributionType == DistributionType.Uniform)
        {
            retVal = SampleUniform(distribution.min, distribution.max);
        }
        else if (distribution.distributionType == DistributionType.Normal)
        {
            retVal = SampleNormal(distribution.mu, distribution.sigma, distribution.min, distribution.max);
        }
        //if inverse
        if (distribution.alternationType == AlternationType.Random && Random.value < 0.5f)
            retVal = -retVal;
        return retVal;
    }
    //generate waypoints by pathSeed，ensure the same in every trial
    public static List<Vector2> GenerateInitialPathByPathSeed(PathSeed pathSeed, float targetDist, out float sumOfDistances, out float sumOfRotations)
    {
        Vector2 initialPosition = Vector2.zero;
        Vector2 initialForward = new Vector2(0, 1);//along z axis
        // THE GENERATION RULE IS WALK THEN TURN! SO THE LAST TURN IS TECHNICALLY REDUNDANT!
        // I'M DOING THIS TO MAKE SURE WE WALK STRAIGHT ALONG THE INITIAL POSITION FIRST BEFORE WE EVER TURN
        List<Vector2> waypoints = new List<Vector2>(pathSeed.waypointCount);
        Vector2 position = initialPosition;
        Vector2 forward = initialForward.normalized;
        Vector2 nextPosition, nextForward;
        float sampledDistance, sampledRotation;
        sumOfDistances = 0;
        sumOfRotations = 0;
        int alternator = 1;

        //add start point
        waypoints.Add(position);
        bool finished = false;
        for (; !finished;)
        {
            sampledDistance = SampleDistribution(pathSeed.distanceDistribution);
            if (sampledDistance + sumOfDistances >= targetDist)
            {
                finished = true;
                sampledDistance = targetDist - sumOfDistances;
            }
            sampledRotation = SampleDistribution(pathSeed.angleDistribution);
            if (pathSeed.angleDistribution.alternationType == AlternationType.Constant)
                sampledRotation *= alternator;
            nextPosition = position + sampledDistance * forward;
            nextForward = Utilities.RotateVector(forward, sampledRotation).normalized; // Normalizing for extra protection in case error accumulates over time
            waypoints.Add(nextPosition);
            position = nextPosition;
            forward = nextForward;
            sumOfDistances += sampledDistance;
            sumOfRotations += Mathf.Abs(sampledRotation); // The last one might seem redundant to add
            alternator *= -1;
        }
        return waypoints;
    }
    //generate circle path
    public static List<Vector2> GenerateCirclePath(float radius, int waypointNum, out float sumOfDistances, out float sumOfRotations, bool if8 = false)
    {
        Vector2 initialPosition = Vector2.zero;
        Vector2 initialForward = new Vector2(0, 1);
        // THE GENERATION RULE IS WALK THEN TURN! SO THE LAST TURN IS TECHNICALLY REDUNDANT!
        // I'M DOING THIS TO MAKE SURE WE WALK STRAIGHT ALONG THE INITIAL POSITION FIRST BEFORE WE EVER TURN
        List<Vector2> waypoints = new List<Vector2>();
        Vector2 position = initialPosition;
        Vector2 forward = initialForward.normalized;
        Vector2 nextPosition;

        waypoints.Add(position);

        sumOfDistances = 0;
        sumOfRotations = 0;

        var center = new Vector2(radius, 0);
        var startVec = -center;
        float sampledRotation = 360f / waypointNum;
        for (int i = 0; i < waypointNum; i++)
        {
            var vec = Utilities.RotateVector(startVec, -sampledRotation * (i + 1));//clockwise
            nextPosition = center + vec;            
            waypoints.Add(nextPosition);
            sumOfDistances += (nextPosition - position).magnitude;
            sumOfRotations += Mathf.Abs(sampledRotation); // The last one might seem redundant to add
            position = nextPosition;
        }
        if (if8) {
            center *= -1;
            startVec *= -1;
            for (int i = 0; i < waypointNum; i++)
            {
                var vec = Utilities.RotateVector(startVec, sampledRotation * (i + 1));
                nextPosition = center + vec;
                waypoints.Add(nextPosition);
                sumOfDistances += (nextPosition - position).magnitude;
                sumOfRotations += Mathf.Abs(sampledRotation); // The last one might seem redundant to add
                position = nextPosition;
            }
        }
        return waypoints;
    }

    public static Vector2 GetRandomPositionWithinBounds(float minX, float maxX, float minZ, float maxZ)
    {
        return new Vector2(SampleUniform(minX, maxX), SampleUniform(minZ, maxZ));
    }

    public static Vector2 GetRandomForward()
    {
        float angle = SampleUniform(0, 360);
        return Utilities.RotateVector(Vector2.up, angle).normalized; // Over-protective with the normalizing
    }

}
