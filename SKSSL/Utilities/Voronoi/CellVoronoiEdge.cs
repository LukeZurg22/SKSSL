namespace SKSSL.Utilities.Voronoi;

internal readonly struct CellVoronoiEdge
{
    public readonly Point SiteA;
    public readonly Point? SiteB;
    public readonly Point Point1;
    public readonly Point Point2;

    public CellVoronoiEdge(Point siteA, Point? siteB, Point point1, Point point2)
    {
        SiteA = siteA;
        SiteB = siteB;
        Point1 = point1;
        Point2 = point2;
    }
}