using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

public record VoronoiCell
{
    public Point Site;
    public List<Point> Vertices; 
}