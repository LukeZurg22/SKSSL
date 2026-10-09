namespace SKSSL.Utilities.Voronoi;

public partial class DelaunayTriangulator
{
    private readonly struct BoundaryEdge
    {
        public readonly Point Point1;
        public readonly Point Point2;

        /*
         * Existing triangle on the other side of the cavity.
         *
         * Null means the edge is on the outer boundary.
         */
        public readonly Triangle? Outside;

        /*
         * Edge index belonging to Outside.
         */
        public readonly int OutsideEdge;

        public BoundaryEdge(Point point1, Point point2, Triangle? outside, int outsideEdge)
        {
            Point1 = point1;
            Point2 = point2;
            Outside = outside;
            OutsideEdge = outsideEdge;
        }
    }
}