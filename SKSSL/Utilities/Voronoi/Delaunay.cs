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
    private readonly HashSet<Triangle> _badTriangles = [];
    private readonly HashSet<Triangle> _visitedTriangles = [];
    private readonly Stack<Triangle> _openTriangles = [];

    private double MaxX { get; set; }
    private double MaxY { get; set; }
    private IEnumerable<Triangle> _border;

    #region Point Creation & Algorithms

    /// <summary>
    /// Generate points using a custom algorithm instead of using the switch of pre-made distributors.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="maxX"></param>
    /// <param name="maxY"></param>
    /// <param name="customSamplingAlgorithm">
    /// A function that takes point count and a list of points as parameters. This is the custom implementation for
    /// randomized points.
    /// </param>
    /// <returns></returns>
    public IEnumerable<Point> GeneratePoints(
        int amount,
        double maxX,
        double maxY,
        Action<int, List<Point>> customSamplingAlgorithm)
    {
        var points = CreatePointsList(maxX, maxY);
        customSamplingAlgorithm(amount, points);
        return points;
    }

    /// <summary>
    /// Create a list of seeded points with varying degrees of spread and randomization.
    /// </summary>
    /// <param name="amount"></param>
    /// <param name="maxX"></param>
    /// <param name="maxY"></param>
    /// <param name="distribution"></param>
    /// <param name="randomness"></param>
    /// <returns></returns>
    public IEnumerable<Point> GeneratePoints(int amount,
        double maxX,
        double maxY,
        PointDistribution distribution, double randomness)
    {
        var points = CreatePointsList(maxX, maxY);

        // Control the random-ness of the points.
        switch (distribution)
        {
            case PointDistribution.RandomJitter:
                RandomJitter(amount, maxX, maxY, points, randomness);
                break;
            case PointDistribution.Custom:
                throw new Exception($"Custom distribution is invalid for defined {nameof(GeneratePoints)} call.");
            // TODO: Add "nudged" random? Remove this todo if the todo about adding modified voronoi masking
            //  in voronoi.cs is solved.
            case PointDistribution.RandomSystem:
            default:
                RandomSystem(amount, points);
                break;
        }

        return points;
    }

    #region Distribution Algorithms

    private static void RandomJitter(
        int amount,
        double maxX,
        double maxY,
        List<Point> points,
        double randomness)
    {
        double aspect = maxX / maxY;
        int columns = (int)Math.Sqrt(amount * aspect);
        int rows = (int)Math.Ceiling((double)amount / columns);
        double cellWidth = maxX / columns;
        double cellHeight = maxY / rows;

        var random = new Random();
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            if (points.Count >= amount)
                break;

            double pX = (x + 0.5 + (random.NextDouble() - 0.5) * randomness) * cellWidth;
            double pY = (y + 0.5 + (random.NextDouble() - 0.5) * randomness) * cellHeight;
            pX = Math.Clamp(pX, 0, maxX);
            pY = Math.Clamp(pY, 0, maxY);
            points.Add(new Point(pX, pY));
        }
    }

    private void RandomSystem(int count, List<Point> points)
    {
        var random = new Random();
        for (int i = 0; i < count - 4; i++)
        {
            var pointX = random.NextDouble() * MaxX;
            var pointY = random.NextDouble() * MaxY;
            points.Add(new Point(pointX, pointY));
        }
    }

    #endregion

    #endregion

    public IEnumerable<Triangle> BowyerWatson(IEnumerable<Point> points)
    {
        var triangulation = new HashSet<Triangle>(_border);
        Triangle start = _border.First();
        foreach (Point point in points)
        {
            // The first four points already form the border.
            if (point == _border.First().Vertices[0] ||
                point == _border.First().Vertices[1] ||
                point == _border.First().Vertices[2])
                continue;

            start = Triangle.FindContainingTriangle(point, start);

            FindBadTriangles(point, start);

            var boundaryEdges =
                FindHoleBoundaries(_badTriangles);

            // Remove the cavity triangles.
            foreach (Triangle triangle in _badTriangles)
            {
                var vertices = triangle.Vertices;
                vertices[0].AdjacentTriangles.Remove(triangle);
                vertices[1].AdjacentTriangles.Remove(triangle);
                vertices[2].AdjacentTriangles.Remove(triangle);
                triangulation.Remove(triangle);
            }

            // Maps a cavity vertex to the newly-created triangle
            // that touches it through the new point.
            var radialTriangles = new Dictionary<Point, Triangle>(
                boundaryEdges.Count);

            foreach (BoundaryEdge boundary in boundaryEdges)
            {
                var newTriangle = new Triangle(
                    point,
                    boundary.Point1,
                    boundary.Point2);

                triangulation.Add(newTriangle);

                // Connect to triangle outside the cavity.
                if (boundary.OutsideTriangle != null)
                {
                    if (boundary.OutsideEdge < 0)
                        throw new InvalidOperationException(
                            "Outside triangle does not contain boundary edge.");

                    int newEdge = newTriangle.IndexOfEdge(boundary.Point1, boundary.Point2);
                    ConnectNeighbors(
                        newTriangle,
                        newEdge,
                        boundary.OutsideTriangle,
                        boundary.OutsideEdge);
                }

                // Connect the radial edge (point, Point1).
                if (radialTriangles.TryGetValue(boundary.Point1, out Triangle? neighbor1))
                {
                    int newEdge = newTriangle.IndexOfEdge(point, boundary.Point1);
                    int neighborEdge = neighbor1.IndexOfEdge(point, boundary.Point1);

                    ConnectNeighbors(
                        newTriangle,
                        newEdge,
                        neighbor1,
                        neighborEdge);
                }
                else radialTriangles.Add(boundary.Point1, newTriangle);

                // Connect the radial edge (point, Point2).
                if (radialTriangles.TryGetValue(boundary.Point2, out Triangle? neighbor2))
                {
                    int newEdge = newTriangle.IndexOfEdge(point, boundary.Point2);
                    int neighborEdge = neighbor2.IndexOfEdge(point, boundary.Point2);
                    ConnectNeighbors(newTriangle, newEdge, neighbor2,
                        neighborEdge);
                }
                else radialTriangles.Add(boundary.Point2, newTriangle);

                // The last-created triangle is a good starting point for the next point.
                start = newTriangle;
            }
        }

        return triangulation;
    }

    #region Helpers

    private static void ConnectNeighbors(Triangle a, int edgeA, Triangle b, int edgeB)
    {
        if (edgeA < 0 || edgeB < 0)
            throw new InvalidOperationException("Attempted to connect triangles across a non-existent edge.");

        a.SetNeighbor(edgeA, b);
        b.SetNeighbor(edgeB, a);
    }

    private static void GetEdgePoints(Triangle triangle, int edge, out Point p1, out Point p2)
    {
        switch (edge)
        {
            case 0:
                p1 = triangle.Vertices[0];
                p2 = triangle.Vertices[1];
                break;

            case 1:
                p1 = triangle.Vertices[1];
                p2 = triangle.Vertices[2];
                break;

            case 2:
                p1 = triangle.Vertices[2];
                p2 = triangle.Vertices[0];
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(edge));
        }
    }

    private void FindBadTriangles(Point point, Triangle start)
    {
        _badTriangles.Clear();
        _visitedTriangles.Clear();
        _openTriangles.Clear();

        _openTriangles.Push(start);

        while (_openTriangles.Count != 0)
        {
            Triangle triangle = _openTriangles.Pop();

            if (!_visitedTriangles.Add(triangle))
                continue;

            if (!triangle.IsPointInsideCircumcircle(point))
                continue;

            _badTriangles.Add(triangle);

            Triangle? neighbor = triangle.Neighbor0;
            if (neighbor != null)
                _openTriangles.Push(neighbor);

            neighbor = triangle.Neighbor1;
            if (neighbor != null)
                _openTriangles.Push(neighbor);

            neighbor = triangle.Neighbor2;
            if (neighbor != null)
                _openTriangles.Push(neighbor);
        }
    }

    private static List<BoundaryEdge> FindHoleBoundaries(HashSet<Triangle> badTriangles)
    {
        var boundaries = new List<BoundaryEdge>(badTriangles.Count * 2);
        foreach (Triangle triangle in badTriangles)
        {
            for (int edge = 0; edge < 3; edge++)
            {
                Triangle? neighbor = triangle.GetNeighbor(edge);

                // This edge is internal to the cavity.
                if (neighbor != null && badTriangles.Contains(neighbor))
                    continue;

                GetEdgePoints(triangle, edge, out Point p1, out Point p2);
                int outsideEdge = neighbor?.IndexOfEdge(p1, p2) ?? -1;
                boundaries.Add(new BoundaryEdge(p1, p2, neighbor, outsideEdge));
            }
        }

        return boundaries;
    }

    private List<Point> CreatePointsList(double maxX, double maxY)
    {
        MaxX = maxX;
        MaxY = maxY;

        var point0 = new Point(0, 0);
        var point1 = new Point(0, MaxY);
        var point2 = new Point(MaxX, MaxY);
        var point3 = new Point(MaxX, 0);

        var points = new List<Point>
        {
            point0,
            point1,
            point2,
            point3
        };

        var tri1 = new Triangle(point0, point1, point2);
        var tri2 = new Triangle(point0, point2, point3);

        ConnectTriangles(tri1, tri2, point0, point2);

        _border = new List<Triangle> { tri1, tri2 };
        return points;
    }

    private static void ConnectTriangles(Triangle a, Triangle b, Point p1, Point p2)
    {
        int edgeA = a.IndexOfEdge(p1, p2);
        int edgeB = b.IndexOfEdge(p1, p2);

        if (edgeA < 0 || edgeB < 0)
            throw new InvalidOperationException("Triangles do not share the specified edge.");

        a.SetNeighbor(edgeA, b);
        b.SetNeighbor(edgeB, a);
    }

    #endregion

    public enum PointDistribution
    {
        Custom = 0,
        RandomSystem = 1,

        /// Accepts evenness parameter.
        RandomJitter = 2,
    }
}

internal struct BoundaryEdge
{
    public readonly Point Point1;
    public readonly Point Point2;
    public readonly Triangle? OutsideTriangle;
    public readonly int OutsideEdge;

    public BoundaryEdge(Point point1, Point point2, Triangle? outsideTriangle, int outsideEdge)
    {
        Point1 = point1;
        Point2 = point2;
        OutsideTriangle = outsideTriangle;
        OutsideEdge = outsideEdge;
    }
}