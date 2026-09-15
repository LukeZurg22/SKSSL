using System;
using System.Collections.Generic;
using static System.Math;

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
public class DelaunayTriangulator : IDisposable
{
    private List<Triangle> _allTriangles = [];
    private List<Triangle> _badTriangles = [];
    private List<BoundaryEdge> _boundaryEdges = [];

    private Stack<Triangle> _openTriangles = [];

    // Reused every insertion. It grows to the largest cavity encountered.
    private Dictionary<Point, Triangle> _radialTriangles = [];

    private int _visitStamp;
    private int _badStamp;

    private int _aliveTriangleCount;

    private double MaxX { get; set; }
    private double MaxY { get; set; }
    private List<Triangle> _border;

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
        var random = new Random();

        // Control the random-ness of the points.
        switch (distribution)
        {
            case PointDistribution.RandomJitter:
                double aspect = maxX / maxY;
                int columns = (int)Sqrt(amount * aspect);
                int rows = (int)Ceiling((double)amount / columns);
                double cellWidth = maxX / columns;
                double cellHeight = maxY / rows;
                for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    if (points.Count >= amount)
                        break;

                    double pX = (x + 0.5 + (random.NextDouble() - 0.5) * randomness) * cellWidth;
                    double pY = (y + 0.5 + (random.NextDouble() - 0.5) * randomness) * cellHeight;
                    pX = Clamp(pX, 0, maxX);
                    pY = Clamp(pY, 0, maxY);
                    points.Add(new Point(pX, pY));
                }

                break;

            case PointDistribution.Custom: throw new Exception($"Custom function not fed to {nameof(GeneratePoints)}.");
            case PointDistribution.RandomSystem:
            default:
                for (int i = 0; i < amount - 4; i++)
                {
                    var pointX = random.NextDouble() * MaxX;
                    var pointY = random.NextDouble() * MaxY;
                    points.Add(new Point(pointX, pointY));
                }

                break;
        }

        return points;
    }

    #endregion

    public IEnumerable<Triangle> BowyerWatson(Point[] points)
    {
        _allTriangles.Clear();
        _badTriangles.Clear();
        _boundaryEdges.Clear();
        _radialTriangles.Clear();
        _openTriangles.Clear();

        Triangle border0 = _border[0];
        Triangle border1 = _border[1];

        border0.Alive = true;
        border1.Alive = true;

        _allTriangles.Add(border0);
        _allTriangles.Add(border1);
        _aliveTriangleCount = 2;

        _visitStamp = 0;
        _badStamp = 0;

        Triangle start = border0;

        // First four points are the rectangle corners and are
        // already represented by the two border triangles.
        for (int pointIndex = 4; pointIndex < points.Length - 1; pointIndex++)
        {
            Point point = points[pointIndex];
            start = FindContainingTriangle(point, start);
            FindBadTriangles(point, start);
            FindHoleBoundaries();

            // Kill cavity triangles.
            foreach (Triangle dead in _badTriangles)
            {
                dead.Alive = false;
                var vertices = dead.Vertices;
                vertices[0].AdjacentTriangles.Remove(dead);
                vertices[1].AdjacentTriangles.Remove(dead);
                vertices[2].AdjacentTriangles.Remove(dead);
            }

            _aliveTriangleCount -= _badTriangles.Count;
            _radialTriangles.Clear();

            // Create replacement triangles.
            foreach (BoundaryEdge boundary in _boundaryEdges)
            {
                var triangle = new Triangle(point, boundary.Point1, boundary.Point2);
                _allTriangles.Add(triangle);
                _aliveTriangleCount++;

                // --------------------------------------------------
                // Boundary edge -> outside triangle.
                // --------------------------------------------------
                Triangle? outside = boundary.Outside;
                if (outside != null)
                {
                    int newEdge = IndexOfEdge(triangle, boundary.Point1, boundary.Point2);
                    int outsideEdge = IndexOfEdge(outside, boundary.Point1, boundary.Point2);
                    if (newEdge < 0 || outsideEdge < 0)
                        throw new InvalidOperationException();

                    SetNeighbor(triangle, newEdge, outside);
                    SetNeighbor(outside, outsideEdge, triangle);
                }

                // --------------------------------------------------
                // Radial edge P -> Point1.
                // --------------------------------------------------
                Point p1 = boundary.Point1;
                if (_radialTriangles.TryGetValue(p1, out Triangle? previous1))
                {
                    int edgeA = IndexOfEdge(triangle, point, p1);
                    int edgeB = IndexOfEdge(previous1, point, p1);
                    SetNeighbor(triangle, edgeA, previous1);
                    SetNeighbor(previous1, edgeB, triangle);
                }
                else _radialTriangles.Add(p1, triangle);

                // --------------------------------------------------
                // Radial edge P -> Point2.
                // --------------------------------------------------
                Point p2 = boundary.Point2;
                if (_radialTriangles.TryGetValue(p2, out Triangle? previous2))
                {
                    int edgeA = IndexOfEdge(triangle, point, p2);
                    int edgeB = IndexOfEdge(previous2, point, p2);
                    SetNeighbor(triangle, edgeA, previous2);
                    SetNeighbor(previous2, edgeB, triangle);
                }
                else _radialTriangles.Add(p2, triangle);

                start = triangle;
            }
        }

        // Compact the live triangles into the result.
        var result = new List<Triangle>(_aliveTriangleCount);

        // ReSharper disable once ForCanBeConvertedToForeach
        for (int i = 0; i < _allTriangles.Count; i++)
        {
            Triangle triangle = _allTriangles[i];
            if (triangle.Alive)
                result.Add(triangle);
        }


        return result;

        // Set a triangle's edge to be the neighbor of another triangle.
        void SetNeighbor(Triangle triangle, int edge, Triangle neighbor)
        {
            switch (edge)
            {
                case 0:
                    triangle.Neighbor0 = neighbor;
                    break;

                case 1:
                    triangle.Neighbor1 = neighbor;
                    break;

                default:
                    triangle.Neighbor2 = neighbor;
                    break;
            }
        }

        // Get int index of triangle edge based on triangle and two points.
        int IndexOfEdge(Triangle t, Point a, Point b)
        {
            Point v0 = t.Vertices[0];
            Point v1 = t.Vertices[1];
            Point v2 = t.Vertices[2];

            if ((v0 == a && v1 == b) || (v0 == b && v1 == a))
                return 0;

            if ((v1 == a && v2 == b) || (v1 == b && v2 == a))
                return 1;

            return 2;
        }

        // Given a point and a triangle starting position, get entire constructed triangle.
        Triangle FindContainingTriangle(Point point, Triangle starting)
        {
            Triangle current = starting;
            for (;;) // Infinite loop.
            {
                Point a = current.Vertices[0];
                Point b = current.Vertices[1];
                Point c = current.Vertices[2];

                double cross =
                    (b.X - a.X) * (point.Y - a.Y) -
                    (b.Y - a.Y) * (point.X - a.X);

                Triangle? next;
                if (cross < 0)
                {
                    next = current.Neighbor0;
                    if (next == null)
                        return current;

                    current = next;
                    continue;
                }

                cross =
                    (c.X - b.X) * (point.Y - b.Y) -
                    (c.Y - b.Y) * (point.X - b.X);

                if (cross < 0)
                {
                    next = current.Neighbor1;
                    if (next == null)
                        return current;

                    current = next;
                    continue;
                }

                cross =
                    (a.X - c.X) * (point.Y - c.Y) -
                    (a.Y - c.Y) * (point.X - c.X);

                if (!(cross < 0))
                    return current;

                next = current.Neighbor2;
                if (next == null)
                    return current;

                current = next;
            }
        }
    }

    #region Helpers

    private void FindBadTriangles(Point point, Triangle start)
    {
        _badTriangles.Clear();
        _openTriangles.Clear();

        int visitStamp = ++_visitStamp;
        int badStamp = ++_badStamp;

        _openTriangles.Push(start);

        while (_openTriangles.Count != 0)
        {
            Triangle triangle = _openTriangles.Pop();

            if (!triangle.Alive)
                continue;

            if (triangle.VisitStamp == visitStamp)
                continue;

            triangle.VisitStamp = visitStamp;

            if (!triangle.IsPointInsideCircumcircle(point))
                continue;

            triangle.BadStamp = badStamp;
            _badTriangles.Add(triangle);

            Triangle? n0 = triangle.Neighbor0;
            if (n0 != null && n0.Alive && n0.VisitStamp != visitStamp)
                _openTriangles.Push(n0);

            Triangle? n1 = triangle.Neighbor1;
            if (n1 != null && n1.Alive && n1.VisitStamp != visitStamp)
                _openTriangles.Push(n1);

            Triangle? n2 = triangle.Neighbor2;
            if (n2 != null && n2.Alive && n2.VisitStamp != visitStamp)
                _openTriangles.Push(n2);
        }
    }

    private void FindHoleBoundaries()
    {
        _boundaryEdges.Clear();

        int badStamp = _badStamp;

        foreach (Triangle triangle in _badTriangles)
        {
            Triangle? n = triangle.Neighbor0;
            if (n == null || n.BadStamp != badStamp)
            {
                _boundaryEdges.Add(new BoundaryEdge(triangle.Vertices[0], triangle.Vertices[1], n));
            }

            n = triangle.Neighbor1;
            if (n == null || n.BadStamp != badStamp)
            {
                _boundaryEdges.Add(new BoundaryEdge(triangle.Vertices[1], triangle.Vertices[2], n));
            }

            n = triangle.Neighbor2;
            if (n == null || n.BadStamp != badStamp)
            {
                _boundaryEdges.Add(new BoundaryEdge(triangle.Vertices[2], triangle.Vertices[0], n));
            }
        }
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

        _border = [tri1, tri2];
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

        /// Produces sharper, more "raw" triangular cells.
        RandomSystem = 1,

        /// Accepts evenness parameter. Produces cleaner cells.
        RandomJitter = 2,
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        _allTriangles = [];
        _badTriangles = [];
        _boundaryEdges = [];
        _openTriangles = [];
        _radialTriangles = [];
        _visitStamp = default;
        _badStamp = default;
        _aliveTriangleCount = default;
        _border = [];
    }
}

internal readonly struct BoundaryEdge
{
    public readonly Point Point1;
    public readonly Point Point2;
    public readonly Triangle? Outside;

    public BoundaryEdge(
        Point point1,
        Point point2,
        Triangle? outside)
    {
        Point1 = point1;
        Point2 = point2;
        Outside = outside;
    }
}