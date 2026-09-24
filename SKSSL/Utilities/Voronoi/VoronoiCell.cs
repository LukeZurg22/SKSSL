using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// Data storage of a cell containing its Site point, Vertex points, and a flag for if it is a boundary.
/// </summary>
public record VoronoiCell
{
    public uint ID => Site.ID;
    public Point Site;
    public List<Point> Vertices;
    public bool IsBoundary;

    public VoronoiCell(Point site, List<Point> vertices)
    {
        Site = site;
        Vertices = vertices;
    }
}