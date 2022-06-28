namespace VisibilityPolygonCSharp
{
    /// <summary>
    ///   Immutable class that holds a segment (two points).
    /// </summary>
    public class Segment<TPoint>
    {
        public Segment(TPoint p1, TPoint p2)
        {
            P1 = p1;
            P2 = p2;
        }

        public TPoint P1 { get; }
        public TPoint P2 { get; }

        internal TPoint this[int index]
        {
            get
            {
                if (index == 0)
                {
                    return P1;
                }
                return P2;
            }
        }
    }
}