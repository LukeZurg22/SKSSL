namespace SKSSL.Utilities.Voronoi;

public partial class DelaunayTriangulator
{
    private readonly struct EdgeReference
    {
        public readonly Triangle Triangle;
        public readonly int Edge;

        public EdgeReference(
            Triangle triangle,
            int edge)
        {
            Triangle = triangle;
            Edge = edge;
        }
    }
}