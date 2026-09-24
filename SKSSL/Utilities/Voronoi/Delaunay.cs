using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static System.Math;

// ReSharper disable CompareOfFloatsByEqualityOperator

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// An implementation of the Delaunay Triangulation algorithm.
/// </summary>
/// <remarks>
/// It operates in these steps:<br/>
/// 1. Saturate with Points.<br/>
/// 2. Construct Voronoi Cells.<br/>
/// 3. Everything else; Color, Shapes, Depth, Noise. The Sky is the limit.
/// </remarks>
/// <references>
/// 1. https://www.redblobgames.com/x/2022-voronoi-maps-tutorial/<br/>
/// 2. https://en.wikipedia.org/wiki/Delaunay_triangulation<br/>
/// 3. https://louis-dr.github.io/voronoimap.html<br/>
/// 4. https://mapbox.github.io/delaunator/
/// </references>
public class DelaunayTriangulator : IDisposable
{
    private readonly List<Triangle> _allTriangles = [];
    private readonly List<Triangle> _badTriangles = [];
    private readonly List<BoundaryEdge> _boundaryEdges = [];
    private readonly Stack<Triangle> _openTriangles = [];

    /*
     * Only contains edges belonging to the currently constructed
     * cavity/new triangles.
     *
     * Edges shared by two new triangles are removed from the map once
     * their second triangle is encountered.
     */
    private readonly Dictionary<ulong, EdgeReference> _edgeMap = new(256);

    private int _visitStamp;
    private int _badStamp;

    private float MaxX { get; set; }
    private float MaxY { get; set; }
    
    #region Algorithm(s)

    /// <summary>
    /// Performs incremental Bowyer-Watson Delaunay triangulation.
    /// </summary>
    /// <remarks>
    /// Topology is updated locally around the cavity instead of rebuilding
    /// the entire triangulation after every inserted point.
    /// </remarks>
    public List<Triangle> BowyerWatson(Point[] points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Length < 5)
        {
            throw new ArgumentException(
                "At least four border points and one interior point are required.",
                nameof(points));
        }

        _allTriangles.Clear();
        _badTriangles.Clear();
        _boundaryEdges.Clear();
        _openTriangles.Clear();
        _edgeMap.Clear();

        _visitStamp = 0;
        _badStamp = 0;

        Point point0 = points[0];
        Point point1 = points[1];
        Point point2 = points[2];
        Point point3 = points[3];

        Triangle border0 = new(point0, point1, point2);
        Triangle border1 = new(point0, point2, point3);

        border0.Alive = true;
        border1.Alive = true;

        _allTriangles.Add(border0);
        _allTriangles.Add(border1);

        ConnectTriangles(
            border0,
            border1,
            point0,
            point2);

        point0.AdjacentTriangles.Add(border0);
        point0.AdjacentTriangles.Add(border1);

        point1.AdjacentTriangles.Add(border0);

        point2.AdjacentTriangles.Add(border0);
        point2.AdjacentTriangles.Add(border1);

        point3.AdjacentTriangles.Add(border1);

        Triangle start = border0;

        /*
         * The first four points are the bounding rectangle.
         *
         * All user/generated points therefore begin at index 4.
         */
        for (int pointIndex = 4;
             pointIndex < points.Length;
             pointIndex++)
        {
            Point point = points[pointIndex];

            if (point.X < 0 ||
                point.X > MaxX ||
                point.Y < 0 ||
                point.Y > MaxY)
            {
                throw new InvalidOperationException(
                    $"Point {pointIndex} ({point.X}, {point.Y}) is outside " +
                    $"the triangulation bounds 0..{MaxX}, 0..{MaxY}.");
            }

            /*
             * Locate the triangle containing the point.
             *
             * The previous containing triangle is used as the starting
             * point, making this considerably faster than searching from
             * the border every time.
             */
            start = FindContainingTriangle(point, start);

            /*
             * Find all triangles whose circumcircles contain the point.
             */
            FindBadTriangles(point, start);
            if (_badTriangles.Count == 0)
                continue;

            /*
             * Determine the cavity boundary before destroying its topology.
             */
            FindHoleBoundaries();

            /*
             * Remove the cavity triangles from the live topology.
             *
             * Only neighboring triangles directly touching the cavity
             * are modified.
             */
            foreach (Triangle bad in _badTriangles)
            {
                RemoveTriangleTopology(bad);
                bad.Alive = false;
            }

            /*
             * The edge map is local to this cavity.
             */
            _edgeMap.Clear();

            Triangle? firstNewTriangle = null;

            /*
             * Fan new triangles around the inserted point.
             */
            foreach (BoundaryEdge boundary in _boundaryEdges)
            {
                Triangle triangle = new(boundary.Point1, boundary.Point2, point)
                {
                    Alive = true
                };

                _allTriangles.Add(triangle);

                AddTriangleTopology(triangle);

                /*
                 * Reconnect this new triangle to the existing triangulation
                 * across the cavity boundary.
                 */
                if (boundary.Outside != null)
                {
                    int newEdge = triangle.IndexOfEdge(
                        boundary.Point1,
                        boundary.Point2);

                    if (newEdge < 0)
                    {
                        throw new InvalidOperationException(
                            "Generated triangle does not contain its " +
                            "cavity boundary edge.");
                    }

                    triangle.SetNeighbor(newEdge, boundary.Outside);
                    boundary.Outside.SetNeighbor(boundary.OutsideEdge, triangle);

                    /*
                     * The boundary edge is no longer open.
                     */
                    _edgeMap.Remove(GetEdgeKey(boundary.Point1, boundary.Point2));
                }

                firstNewTriangle ??= triangle;
            }

            /*
             * The new triangle is an excellent starting point for the
             * next point-location search.
             */
            start = firstNewTriangle ?? throw new InvalidOperationException(
                $"No replacement triangles were generated for point " +
                $"{pointIndex} ({point.X}, {point.Y}).");

#if DEBUG
            /*
             * Optional expensive validation.
             *
             * Keep this disabled in Release builds. The whole point of
             * the optimized implementation is to avoid rebuilding the
             * entire topology every iteration.
             */
            ValidateLocalTopology(_boundaryEdges);
#endif
        }

        /*
         * Dead triangles are retained during construction so that removal
         * never causes O(N) List shifting. Remove them once at the end.
         */
        _allTriangles.RemoveAll(static triangle => !triangle.Alive);
        return _allTriangles;
    }

    /// <summary>
    /// Performs one partial Lloyd relaxation pass.
    /// </summary>
    public void LloydSettlePoints(ICollection<VoronoiCell> cells)
    {
        foreach (VoronoiCell cell in cells)
        {
            Point point = cell.Site;

            // The first four points are the artificial bounding rectangle.
            if (point.ID < 4)
                continue;

            if (cell.Vertices.Count == 0)
                continue;

            float x = 0;
            float y = 0;

            foreach (Point vertex in cell.Vertices)
            {
                x += vertex.X;
                y += vertex.Y;
            }

            float count = cell.Vertices.Count;

            x /= count;
            y /= count;

            cell.Site = point with { X = (float)Clamp(x, 0.0, MaxX),  Y = (float)Clamp(y, 0.0, MaxY)};
        }
    }

    #endregion

    #region Cavity Search

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FindBadTriangles(Point point, Triangle start)
    {
        _badTriangles.Clear();
        _openTriangles.Clear();

        int visitStamp = ++_visitStamp;
        int badStamp = ++_badStamp;

        _openTriangles.Push(start);

        while (_openTriangles.Count > 0)
        {
            Triangle triangle = _openTriangles.Pop();
            if (!triangle.Alive || triangle.VisitStamp == visitStamp)
                continue;

            triangle.VisitStamp = visitStamp;
            if (!triangle.IsPointInsideCircumcircle(point))
                continue;

            triangle.BadStamp = badStamp;
            _badTriangles.Add(triangle);

            Triangle? neighbor = triangle.Neighbor0;
            if (neighbor != null && neighbor.Alive && neighbor.VisitStamp != visitStamp)
            {
                _openTriangles.Push(neighbor);
            }

            neighbor = triangle.Neighbor1;
            if (neighbor != null && neighbor.Alive && neighbor.VisitStamp != visitStamp)
            {
                _openTriangles.Push(neighbor);
            }

            neighbor = triangle.Neighbor2;
            if (neighbor != null && neighbor.Alive && neighbor.VisitStamp != visitStamp)
            {
                _openTriangles.Push(neighbor);
            }
        }
    }

    private void FindHoleBoundaries()
    {
        _boundaryEdges.Clear();

        int badStamp = _badStamp;
        foreach (Triangle triangle in _badTriangles)
        {
            /*
             * The triangle is still alive at this point. Its neighbors
             * are therefore still valid and can be inspected.
             */
            for (int edge = 0; edge < 3; edge++)
            {
                Point a = GetEdgeA(triangle, edge);
                Point b = GetEdgeB(triangle, edge);
                Triangle? outside = GetNeighbor(triangle, edge);

                /*
                 * No neighbor means this edge is on the outer boundary.
                 */
                if (outside == null)
                {
                    _boundaryEdges.Add(new BoundaryEdge(a, b, null, -1));
                    continue;
                }

                /*
                 * The neighboring triangle is also inside the cavity.
                 * Therefore this edge is internal and is discarded.
                 */
                if (outside.BadStamp == badStamp)
                    continue;

                int outsideEdge =
                    outside.IndexOfEdge(a, b);

                if (outsideEdge < 0)
                {
                    throw new InvalidOperationException(
                        $"Neighbor relationship is invalid. " +
                        $"Triangle {outside.Id} does not contain edge " +
                        $"({a.X}, {a.Y}) - ({b.X}, {b.Y}).");
                }

                _boundaryEdges.Add(new BoundaryEdge(a, b, outside, outsideEdge));
            }
        }
    }

    private Triangle FindContainingTriangle(Point p, Triangle initial)
    {
        Triangle current = initial;
        int stamp = ++_visitStamp;
        while (true)
        {
            if (!current.Alive)
            {
                throw new InvalidOperationException(
                    $"FindContainingTriangle reached dead triangle " +
                    $"{current.Id}.");
            }

            /*
             * No HashSet allocation is necessary.
             */
            if (current.VisitStamp == stamp)
            {
                throw new InvalidOperationException(
                    $"FindContainingTriangle cycled for Point " +
                    $"({p.X}, {p.Y}). Triangle = " +
                    $"{current.Vertices[0].ID}-" +
                    $"{current.Vertices[1].ID}-" +
                    $"{current.Vertices[2].ID}");
            }

            current.VisitStamp = stamp;

            Point a = current.Vertices[0];
            Point b = current.Vertices[1];
            Point c = current.Vertices[2];

            if (Cross(a, b, p) < 0)
            {
                current = current.Neighbor0 ?? throw OutsideTriangle(p, current, 0);
                continue;
            }

            if (Cross(b, c, p) < 0)
            {
                current = current.Neighbor1 ?? throw OutsideTriangle(p, current, 1);
                continue;
            }

            if (!(Cross(c, a, p) < 0))
                return current;

            current = current.Neighbor2 ?? throw OutsideTriangle(p, current, 2);
        }
    }

    private static InvalidOperationException OutsideTriangle(Point p, Triangle triangle, int edge)
        => new($"Point ({p.X}, {p.Y}) lies outside edge {edge} of triangle {triangle.Id}.");

    #endregion

    #region Topology

    /// <summary>
    /// Removes a triangle from the local live topology.
    /// </summary>
    private static void RemoveTriangleTopology(
        Triangle triangle)
    {
        Triangle? n0 = triangle.Neighbor0;
        Triangle? n1 = triangle.Neighbor1;
        Triangle? n2 = triangle.Neighbor2;

        if (n0 != null)
            RemoveNeighbor(n0, triangle);

        if (n1 != null && n1 != n0)
            RemoveNeighbor(n1, triangle);

        if (n2 != null && n2 != n0 && n2 != n1)
            RemoveNeighbor(n2, triangle);

        triangle.Neighbor0 = null;
        triangle.Neighbor1 = null;
        triangle.Neighbor2 = null;

        triangle.Vertices[0]
            .AdjacentTriangles
            .Remove(triangle);

        triangle.Vertices[1]
            .AdjacentTriangles
            .Remove(triangle);

        triangle.Vertices[2]
            .AdjacentTriangles
            .Remove(triangle);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void RemoveNeighbor(Triangle triangle, Triangle neighbor)
    {
        if (triangle.Neighbor0 == neighbor)
        {
            triangle.Neighbor0 = null;
            return;
        }

        if (triangle.Neighbor1 == neighbor)
        {
            triangle.Neighbor1 = null;
            return;
        }

        if (triangle.Neighbor2 == neighbor)
        {
            triangle.Neighbor2 = null;
        }
    }

    /// <summary>
    /// Adds a newly created triangle to the local topology.
    /// </summary>
    private void AddTriangleTopology(Triangle triangle)
    {
        triangle.Neighbor0 = null;
        triangle.Neighbor1 = null;
        triangle.Neighbor2 = null;

        triangle.Vertices[0]
            .AdjacentTriangles
            .Add(triangle);

        triangle.Vertices[1]
            .AdjacentTriangles
            .Add(triangle);

        triangle.Vertices[2]
            .AdjacentTriangles
            .Add(triangle);

        for (int edge = 0; edge < 3; edge++)
        {
            Point a = GetEdgeA(triangle, edge);
            Point b = GetEdgeB(triangle, edge);

            ulong key = GetEdgeKey(a, b);

            if (!_edgeMap.TryGetValue(key, out EdgeReference other))
            {
                _edgeMap.Add(key, new EdgeReference(triangle, edge));
                continue;
            }

            Triangle otherTriangle = other.Triangle;

            /*
             * A dead triangle should never normally be present in this
             * map, but replacing it makes this routine robust against
             * accidental stale state.
             */
            if (!otherTriangle.Alive)
            {
                _edgeMap[key] = new EdgeReference(triangle, edge);
                continue;
            }

            /*
             * Two live triangles share this edge.
             */
            triangle.SetNeighbor(edge, otherTriangle);
            otherTriangle.SetNeighbor(other.Edge, triangle);

            /*
             * It is no longer an open edge.
             */
            _edgeMap.Remove(key);
        }
    }

    // ReSharper disable once UnusedMember.Local
    private static void ResetTriangle(Triangle triangle)
    {
        triangle.Alive = true;

        triangle.VisitStamp = 0;
        triangle.BadStamp = 0;

        triangle.Neighbor0 = null;
        triangle.Neighbor1 = null;
        triangle.Neighbor2 = null;
    }

    private static void ConnectTriangles(Triangle a, Triangle b, Point p1, Point p2)
    {
        int edgeA = a.IndexOfEdge(p1, p2);
        int edgeB = b.IndexOfEdge(p1, p2);

#if DEBUG
        if (edgeA < 0 || edgeB < 0)
            throw new InvalidOperationException(
                "Triangles do not share the specified edge.");
#endif

        a.SetNeighbor(edgeA, b);
        b.SetNeighbor(edgeB, a);
    }

    #endregion

    #region Geometry

    internal List<Point> CreatePointsList(float maxX, float maxY)
    {
        MaxX = maxX;
        MaxY = maxY;

        Point point0 = new(0, 0);
        Point point1 = new(0, maxY);
        Point point2 = new(maxX, maxY);
        Point point3 = new(maxX, 0);

        var points = new List<Point>(4)
        {
            point0,
            point1,
            point2,
            point3
        };

        Triangle tri1 = new(point0, point1, point2);
        Triangle tri2 = new(point0, point2, point3);

        ConnectTriangles(tri1, tri2, point0, point2);
        return points;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Cross(Point a, Point b, Point c) => (b.X - a.X) *
                                                              (c.Y - a.Y)
                                                              -
                                                              (b.Y - a.Y) *
                                                              (c.X - a.X);

    #endregion

    #region Edge Operations

    /*
     * A point ID is used rather than its floating-point coordinates.
     *
     * This is both faster and safer:
     *
     *     A-B == B-A
     *
     * and the dictionary key is only one ulong.
     *
     * This assumes Point.ID uniquely identifies a point.
     */
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong GetEdgeKey(Point a, Point b)
    {
        uint idA = a.ID;
        uint idB = b.ID;

        if (idA > idB)
            (idA, idB) = (idB, idA);

        return ((ulong)idA << 32) | idB;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point GetEdgeA(Triangle triangle, int edge) => edge switch
    {
        0 => triangle.Vertices[0],
        1 => triangle.Vertices[1],
        _ => triangle.Vertices[2]
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point GetEdgeB(Triangle triangle, int edge) => edge switch
    {
        0 => triangle.Vertices[1],
        1 => triangle.Vertices[2],
        _ => triangle.Vertices[0]
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Triangle? GetNeighbor(Triangle triangle, int edge) => edge switch
    {
        0 => triangle.Neighbor0,
        1 => triangle.Neighbor1,
        2 => triangle.Neighbor2,
        _ => throw new ArgumentOutOfRangeException(nameof(edge))
    };

    #endregion

    #region Validation

#if DEBUG

    /// <summary>
    /// Performs a full topology rebuild and compares it against the
    /// incrementally maintained topology.
    ///
    /// This is intentionally DEBUG-only because it defeats the primary
    /// performance optimization of this implementation.
    /// </summary>
    private static void ValidateLocalTopology(List<BoundaryEdge> boundaries)
    {
        /*
         * The optimized algorithm intentionally doesn't perform a global
         * rebuild here.
         *
         * This method is deliberately lightweight. The full
         * RebuildNeighbors() method below can be called manually when
         * debugging more serious topology problems.
         */
        foreach (BoundaryEdge boundary in boundaries)
        {
            if (boundary.Outside == null)
                continue;

            if (!boundary.Outside.Alive)
                throw new InvalidOperationException(
                    "A cavity boundary references a dead triangle.");

            int edge =
                boundary.Outside.IndexOfEdge(
                    boundary.Point1,
                    boundary.Point2);

            if (edge < 0)
                throw new InvalidOperationException(
                    "Cavity boundary outside triangle does not contain the expected boundary edge.");

            Triangle? neighbor = GetNeighbor(boundary.Outside, edge);

            if (neighbor is not { Alive: true })
                throw new InvalidOperationException("Cavity boundary was not reconnected correctly.");
        }
    }

    /// <summary>
    /// Full topology rebuild intended for debugging/validation only.
    /// Never call this during normal triangulation.
    /// </summary>
    // ReSharper disable once UnusedMember.Local
    private void RebuildNeighbors()
    {
        var edges = new Dictionary<ulong, EdgeReference>(_allTriangles.Count * 3);
        foreach (Triangle triangle in _allTriangles)
        {
            if (!triangle.Alive)
                continue;

            triangle.Neighbor0 = null;
            triangle.Neighbor1 = null;
            triangle.Neighbor2 = null;

            for (int edge = 0; edge < 3; edge++)
            {
                Point a = GetEdgeA(triangle, edge);
                Point b = GetEdgeB(triangle, edge);

                ulong key = GetEdgeKey(a, b);

                if (!edges.TryGetValue(key, out EdgeReference other))
                {
                    edges.Add(key, new EdgeReference(triangle, edge));
                    continue;
                }

                Triangle first = other.Triangle;
                if (first == triangle)
                    throw new InvalidOperationException($"Triangle {triangle.Id} contains a duplicate edge.");

                if (GetNeighbor(first, other.Edge) != null)
                    throw new InvalidOperationException($"More than two live triangles share edge " +
                                                        $"({a.X}, {a.Y}) - ({b.X}, {b.Y}).");

                if (GetNeighbor(triangle, edge) != null)
                    throw new InvalidOperationException($"Triangle {triangle.Id} already has neighbor on edge {edge}.");

                first.SetNeighbor(other.Edge, triangle);
                triangle.SetNeighbor(edge, first);
            }
        }
    }

#endif

    #endregion

    #region Types

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

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _allTriangles.Clear();
        _badTriangles.Clear();
        _boundaryEdges.Clear();
        _openTriangles.Clear();
        _edgeMap.Clear();

        _visitStamp = 0;
        _badStamp = 0;

        MaxX = 0;
        MaxY = 0;

        GC.SuppressFinalize(this);
    }

    #endregion
}