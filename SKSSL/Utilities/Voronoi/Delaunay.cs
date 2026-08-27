using System;
using System.Collections.Generic;
using System.Linq;

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// An implementation of the Delaunay Triangulation algorithm.
/// </summary>
/// <remarks>
/// It operates in these steps:<br/>
/// 1. Saturate with Points.<br/>
/// 2. Construct Voronoi Cells.<br/>...<br/>
/// n. Everything else; Color, Shapes, Depth, Noise. The Sky is the limit.
/// </remarks>
/// <references>
/// 1. https://www.redblobgames.com/x/2022-voronoi-maps-tutorial/<br/>
/// 2. https://en.wikipedia.org/wiki/Delaunay_triangulation<br/>
/// 3. https://louis-dr.github.io/voronoimap.html<br/>
/// 4. https://mapbox.github.io/delaunator/
/// </references>
public class DelaunayTriangulator
{
    private double MaxX { get; set; }
    private double MaxY { get; set; }
    private IEnumerable<Triangle> border;

    public IEnumerable<Point> GeneratePoints(int amount, double maxX, double maxY)
    {
        MaxX = maxX;
        MaxY = maxY;

        // TODO make more beautiful
        var point0 = new Point(0, 0);
        var point1 = new Point(0, MaxY);
        var point2 = new Point(MaxX, MaxY);
        var point3 = new Point(MaxX, 0);
        var points = new List<Point> { point0, point1, point2, point3 };
        var tri1 = new Triangle(point0, point1, point2);
        var tri2 = new Triangle(point0, point2, point3);
        border = new List<Triangle> { tri1, tri2 };

        var random = new Random();
        for (int i = 0; i < amount - 4; i++)
        {
            var pointX = random.NextDouble() * MaxX;
            var pointY = random.NextDouble() * MaxY;
            points.Add(new Point(pointX, pointY));
        }

        return points;
    }

    public IEnumerable<Triangle> BowyerWatson(IEnumerable<Point> points)
    {
        //var supraTriangle = GenerateSupraTriangle();
        var triangulation = new HashSet<Triangle>(border);

        foreach (Point point in points)
        {
            var badTriangles = FindBadTriangles(point, triangulation);
            var polygon = FindHoleBoundaries(badTriangles);

            foreach (Triangle triangle in badTriangles)
            foreach (Point vertex in triangle.Vertices)
                vertex.AdjacentTriangles.Remove(triangle);

            triangulation.RemoveWhere(o => badTriangles.Contains(o));
            foreach (Edge edge in polygon.Where(possibleEdge
                         => possibleEdge.Point1 != point && possibleEdge.Point2 != point))
            {
                triangulation.Add(new Triangle(point, edge.Point1, edge.Point2));
            }
        }

        //triangulation.RemoveWhere(o => o.Vertices.Any(v => supraTriangle.Vertices.Contains(v)));
        return triangulation;
    }

    private static List<Edge> FindHoleBoundaries(ISet<Triangle> badTriangles)
    {
        var edges = new List<Edge>();
        foreach (Triangle triangle in badTriangles)
        {
            edges.Add(new Edge(triangle.Vertices[0], triangle.Vertices[1]));
            edges.Add(new Edge(triangle.Vertices[1], triangle.Vertices[2]));
            edges.Add(new Edge(triangle.Vertices[2], triangle.Vertices[0]));
        }

        var boundaryEdges = edges.GroupBy(o => o)
            .Where(o => o.Count() == 1)
            .Select(o => o.First());
        return boundaryEdges.ToList();
    }

    [Obsolete]
    // ReSharper disable once UnusedMember.Local
    private Triangle GenerateSupraTriangle()
    {
        //   1  -> maxX
        //  / \
        // 2---3
        // |
        // v maxY
        const int margin = 500;
        var point1 = new Point(0.5 * MaxX, -2 * MaxX - margin);
        var point2 = new Point(-2 * MaxY - margin, 2 * MaxY + margin);
        var point3 = new Point(2 * MaxX + MaxY + margin, 2 * MaxY + margin);
        return new Triangle(point1, point2, point3);
    }

    /// Finds bad triangles.
    private static HashSet<Triangle> FindBadTriangles(Point point, HashSet<Triangle> triangles)
        => [..triangles.Where(o => o.IsPointInsideCircumcircle(point))];
}