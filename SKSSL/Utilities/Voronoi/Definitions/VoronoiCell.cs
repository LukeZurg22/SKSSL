using System.Collections.Generic;
using Clipper2Lib;

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

    /// <summary>
    /// Geometry used for rendering after gap clipping.
    /// Null means the original Vertices should be used.
    /// </summary>
    public Paths64? RenderPaths { get; set; }

    public bool IsCulled { get; set; }

    public VoronoiCell(Point site, List<Point> vertices)
    {
        Site = site;
        Vertices = vertices;
    }
}