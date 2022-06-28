using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VisibilityPolygonCSharp
{
    /// <summary>
    ///   A sample point adapter for the sample point 
    /// </summary>
    public class Vector2Adapter : PointAdapter<Vector2>
    {
        public override double GetX(Vector2 point)
        {
            return (double) point.x;
        }

        public override double GetY(Vector2 point)
        {
            return (double) point.y;
        }

        public override Vector2 Create(double x, double y)
        {
            return new Vector2((float) x, (float) y);
        }
    }
}