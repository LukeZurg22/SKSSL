using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clipper2Lib;
using LibTessDotNet.Double;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static SKSSL.Mathematics.Indexers;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private void AssignSpatialIds(List<Point> points)
    {
        var realPoints = points
            .Skip(4)
            .OrderBy(p => Hilbert(p.X, p.Y, _width, _height))
            .ToList();

        uint realCount = (uint)realPoints.Count;
        _firstArtificialId = realCount;

        Point artificial0 = points[0];
        Point artificial1 = points[1];
        Point artificial2 = points[2];
        Point artificial3 = points[3];

        points.Clear();

        // Artificial points must remain at indexes 0..3.
        points.Add(new Point(artificial0.X, artificial0.Y, realCount));
        points.Add(new Point(artificial1.X, artificial1.Y, realCount + 1));
        points.Add(new Point(artificial2.X, artificial2.Y, realCount + 2));
        points.Add(new Point(artificial3.X, artificial3.Y, realCount + 3));

        // Real points get spatially ordered IDs 0..N-1.
        for (uint i = 0; i < realCount; i++)
        {
            Point p = realPoints[(int)i];
            points.Add(new Point(p.X, p.Y, i));
        }
    }

    private static void SortVerticesAround(List<Point> vertices, Point center)
    {
        vertices.Sort((a, b) =>
        {
            float ax = a.X - center.X;
            float ay = a.Y - center.Y;
            float bx = b.X - center.X;
            float by = b.Y - center.Y;

            bool aUpper = ay > 0 || (ay == 0 && ax >= 0);
            bool bUpper = by > 0 || (by == 0 && bx >= 0);

            if (aUpper != bUpper)
                return aUpper ? -1 : 1;

            float cross = ax * by - ay * bx;
            return cross > 0 ? -1 : cross < 0 ? 1 : 0;
        });
    }

    internal static bool IsPointInsideCell(VoronoiCell cell, int pointX, int pointY)
    {
        // If the cell has been gap-clipped, test the clipped geometry instead of the original Voronoi polygon.
        if (cell.RenderPaths is not { Count: > 0 } paths)
            return cell.Vertices.Count >= 3 && PointInPolygon(cell.Vertices, pointX, pointY);

        bool inside = false;
        foreach (Path64 path in paths)
        {
            if (path.Count < 3)
                continue;

            if (PointInPolygon(path, pointX, pointY))
                inside = !inside;
        }

        return inside;
    }

    private static bool PointInPolygon(List<Point> polygon, double x, double y)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Point a = polygon[i];
            Point b = polygon[j];
            if (PointOnSegment(a.X, a.Y, b.X, b.Y, x, y))
                return true;

            if (Crosses(x, y, a, b))
                inside = !inside;
        }

        return inside;
    }

    private static bool PointInPolygon(Path64 polygon, double x, double y)
    {
        bool inside = false;
        int count = polygon.Count;
        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Point64 a = polygon[i];
            Point64 b = polygon[j];

            if (PointOnSegment(a.X, a.Y, b.X, b.Y, x, y))
                return true;

            if (Crosses(x, y, a, b))
                inside = !inside;
        }

        return inside;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Crosses(double x, double y, Point64 a, Point64 b)
        => a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Crosses(double x, double y, Point a, Point b)
        => a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X;

    private static bool PointOnSegment(double ax, double ay, double bx, double by, double px, double py)
    {
        double cross = (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        if (Math.Abs(cross) > 0.000001)
            return false;
        return px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) && py >= Math.Min(ay, by) && py <= Math.Max(ay, by);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetCellIdFromSiteId(uint siteId)
        => siteId >= (uint)_cellIdBySiteId.Length ? VoronoiCell.InvalidId : _cellIdBySiteId[siteId];

    private static Point IntersectVertical(Point a, Point b, float x)
    {
        var dx = b.X - a.X;
        if (Math.Abs(dx) < 0.000001f)
            return new Point((int)Math.Round(x), a.Y);

        var t = (x - a.X) / dx;
        var y = a.Y + (b.Y - a.Y) * t;

        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }

    private static Point IntersectHorizontal(Point a, Point b, float y)
    {
        var dy = b.Y - a.Y;
        if (Math.Abs(dy) < 0.000001f)
            return new Point(a.X, (int)Math.Round(y));

        var t = (y - a.Y) / dy;
        var x = a.X + (b.X - a.X) * t;
        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }


    /// <remarks>Needed for debug in order to confirm nothings gone wrong.</remarks>
    private static void ValidateNeighbors(List<Triangle> triangles)
    {
        foreach (Triangle t in triangles)
        {
            Validate(t, 0, t.Neighbor0);
            Validate(t, 1, t.Neighbor1);
            Validate(t, 2, t.Neighbor2);
        }

        return;

        void Validate(Triangle t, int edge, Triangle? n)
        {
            if (n == null)
                return;

            Point a;
            Point b;

            switch (edge)
            {
                case 0:
                    a = t.Vertices[0];
                    b = t.Vertices[1];
                    break;

                case 1:
                    a = t.Vertices[1];
                    b = t.Vertices[2];
                    break;

                default:
                    a = t.Vertices[2];
                    b = t.Vertices[0];
                    break;
            }

            int reciprocal = n.IndexOfEdge(a, b);
            if (reciprocal < 0)
                throw new InvalidOperationException("Neighbor does not share the expected edge.");

            Triangle? reverse = reciprocal switch
            {
                0 => n.Neighbor0,
                1 => n.Neighbor1,
                _ => n.Neighbor2
            };

            if (!ReferenceEquals(reverse, t))
                throw new InvalidOperationException("Neighbor relationship is not reciprocal.");
        }
    }


    /// <summary>
    /// Marks whether a cell at a given site should be culled.
    /// </summary>
    /// <param name="site">Point at which a cell is expected to be.</param>
    /// <returns>
    /// True if the culling mode is <see cref="VoronoiBoundaryMode.CulledSquare"/>, the point belongs to a valid cell,
    /// and that cell is a boundary cell or simply has no vertices. Otherwise... it returns false.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool ShouldCullBoundarySite(Point site)
    {
        if (IsArtificialBoundarySite(site))
            return true;

        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (_boundaryMode is VoronoiBoundaryMode.CulledSquare or VoronoiBoundaryMode.CulledCircular &&
            _voronoiCells.TryGetValue(site, out VoronoiCell? cell) &&
            (cell.IsBoundary || cell.Vertices.Count == 0))
            return true;

        return false;
    }

    /// Map geometry is non-negative, so this is equivalent to Math.Round(value)
    /// for the normal coordinate range while avoiding the comparatively expensive
    /// Math.Round call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long FastRoundToLong(double value) => (long)(value + 0.5);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddVoronoiEdges(Triangle triangle, VoronoiBoundaryMode boundaryMode)
    {
        // Edge 0: vertices 0 -> 1
        AddVoronoiEdge(triangle, triangle.Neighbor0, triangle.Vertices[0], triangle.Vertices[1], boundaryMode);

        // Edge 1: vertices 1 -> 2
        AddVoronoiEdge(triangle, triangle.Neighbor1, triangle.Vertices[1], triangle.Vertices[2], boundaryMode);

        // Edge 2: vertices 2 -> 0
        AddVoronoiEdge(triangle, triangle.Neighbor2, triangle.Vertices[2], triangle.Vertices[0], boundaryMode);
    }

    private void AddVoronoiEdge(Triangle triangle, Triangle? neighbor, Point a, Point b, VoronoiBoundaryMode mode)
    {
        Point start;
        Point end;
        if (mode == VoronoiBoundaryMode.HardEdgeClosed)
        {
            // Artificial sites are triangulation scaffolding only.
            if (IsArtificialBoundarySite(a) || IsArtificialBoundarySite(b))
                return;

            // INTERNAL REAL-REAL EDGE
            if (neighbor != null && !IsArtificialBoundaryTriangle(neighbor))
            {
                // Only emit each internal Voronoi edge once.
                if (triangle.Id >= neighbor.Id)
                    return;

                Point p1 = triangle.Circumcenter;
                Point p2 = neighbor.Circumcenter;

                if (!ClipLineToBounds(p1, p2, out Point clipped1, out Point clipped2))
                    return;

                _voronoiEdges.Add(new Edge(clipped1, clipped2));
                _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, clipped1, clipped2));
                return;
            }

            // HULL / BOUNDARY EDGE
            //
            // HardEdgeClosed replaces the infinite Voronoi ray with the
            // finite ray from the circumcenter to the screen boundary.
            if (!TryGetBoundaryVoronoiEdge(triangle, a, b, out start, out end))
                return;

            _voronoiEdges.Add(new Edge(start, end));
            _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, start, end));
            return;
        }


        // Existing Culled / HardEdgeOpen behavior.
        if (neighbor != null)
        {
            if (triangle.Id >= neighbor.Id)
                return;

            if (mode is VoronoiBoundaryMode.CulledSquare or VoronoiBoundaryMode.CulledCircular
                && (ShouldCullBoundarySite(a) || ShouldCullBoundarySite(b)))
                return;

            Point p1 = triangle.Circumcenter;
            Point p2 = neighbor.Circumcenter;

            if (!ClipLineToBounds(p1, p2, out Point clipped1, out Point clipped2))
                return;

            _voronoiEdges.Add(new Edge(clipped1, clipped2));
            _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, clipped1, clipped2));
            return;
        }

        if (mode == VoronoiBoundaryMode.CulledSquare)
            return;

        if (IsGapCulledBoundaryEdge(a, b))
            return;

        if (!TryGetBoundaryVoronoiEdge(triangle, a, b, out start, out end))
            return;

        _voronoiEdges.Add(new Edge(start, end));
        _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, start, end));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsGapCulledBoundaryEdge(Point a, Point b)
    {
        bool aRenderable = _voronoiCells.TryGetValue(a, out VoronoiCell? cellA) && HasRenderableCellGeometry(cellA);
        bool bRenderable = _voronoiCells.TryGetValue(b, out VoronoiCell? cellB) && HasRenderableCellGeometry(cellB);
        return !aRenderable && !bRenderable;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasRenderableCellGeometry(VoronoiCell cell)
        => !cell.IsCulled && !IsArtificialBoundarySite(cell.Site) &&
           (cell.RenderPaths is not null ? cell.RenderPaths.Count != 0 : cell.Vertices.Count >= 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsArtificialBoundarySite(Point site) => site.ID >= _firstArtificialId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsArtificialBoundaryTriangle(Triangle triangle)
        => IsArtificialBoundarySite(triangle.Vertices[0]) ||
           IsArtificialBoundarySite(triangle.Vertices[1]) ||
           IsArtificialBoundarySite(triangle.Vertices[2]);

    private bool TryGetBoundaryVoronoiEdge(
        Triangle triangle,
        Point a,
        Point b,
        out Point start,
        out Point end)
    {
        Vector2 origin = new(triangle.Circumcenter.X, triangle.Circumcenter.Y);

        Vector2 va = new(a.X, a.Y);
        Vector2 vb = new(b.X, b.Y);
        Vector2 edge = vb - va;

        if (edge.LengthSquared() < 0.000001f)
        {
            start = default;
            end = default;
            return false;
        }

        /*
         * There are two possible normals to the Delaunay edge.
         *
         * Pick the one pointing AWAY from the third vertex of the
         * triangle. That is the direction of the unbounded Voronoi ray.
         */

        Vector2 normal = new(-edge.Y, edge.X);
        normal.Normalize();
        Vector2 midpoint = (va + vb) * 0.5f;
        Point thirdPoint;
        if (triangle.Vertices[0] != a && triangle.Vertices[0] != b)
        {
            thirdPoint = triangle.Vertices[0];
        }
        else if (triangle.Vertices[1] != a && triangle.Vertices[1] != b)
        {
            thirdPoint = triangle.Vertices[1];
        }
        else
        {
            thirdPoint = triangle.Vertices[2];
        }

        Vector2 third = new(thirdPoint.X, thirdPoint.Y);

        // Make normal point away from the triangle.
        if (Vector2.Dot(normal, third - midpoint) > 0f)
            normal = -normal;

        /*
         * Intersect the ray:
         *
         *     origin + normal * t
         *
         * with the diagram rectangle.
         *
         * This gives us the portion of the infinite Voronoi ray
         * that is actually visible inside the diagram.
         */

        float tMin = 0f;
        float tMax = float.MaxValue;

        if (!ClipRayAxis(origin.X, normal.X, 0f, _width, ref tMin, ref tMax))
        {
            start = default;
            end = default;
            return false;
        }

        if (!ClipRayAxis(origin.Y, normal.Y, 0f, _height, ref tMin, ref tMax))
        {
            start = default;
            end = default;
            return false;
        }

        if (tMax < tMin || tMax < 0f)
        {
            start = default;
            end = default;
            return false;
        }

        tMin = Math.Max(tMin, 0f);

        Vector2 p1 = origin + normal * tMin;
        Vector2 p2 = origin + normal * tMax;

        //@formatter:off
        start = new Point((int)Math.Round(Math.Clamp(p1.X, 0f, _width)), (int)Math.Round(Math.Clamp(p1.Y, 0f, _height)));
        end = new Point((int)Math.Round(Math.Clamp(p2.X, 0f, _width)), (int)Math.Round(Math.Clamp(p2.Y, 0f, _height)));
        //@formatter:on

        return start != end;
    }

    #region TESSELATION

    private static Vector3[] TessellateOriginalCellPositions(List<Point> vertices)
    {
        int count = vertices.Count;
        if (count < 3)
            return [];

        var result = new Vector3[(count - 2) * 3];
        ReadOnlySpan<Point> points = CollectionsMarshal.AsSpan(vertices);
        Point origin = points[0];
        Vector3 originPosition = new(origin.X, origin.Y, 0f);
        int index = 0;
        for (int i = 1; i < count - 1; i++)
        {
            Point b = points[i];
            Point c = points[i + 1];
            result[index++] = originPosition;
            result[index++] = new Vector3(b.X, b.Y, 0f);
            result[index++] = new Vector3(c.X, c.Y, 0f);
        }

        return result;
    }

    private static Vector3[] TessellateClippedCellPositions(Paths64 paths)
    {
        if (paths.Count == 0)
            return [];

        var tess = new Tess();
        foreach (Path64 path in paths)
        {
            if (path.Count < 3)
                continue;

            var contour = new ContourVertex[path.Count];
            for (int i = 0; i < path.Count; i++)
            {
                Point64 point = path[i];
                contour[i].Position = new Vec3(point.X, point.Y, 0.0);
            }

            tess.AddContour(contour);
        }

        tess.Tessellate();

        if (tess.ElementCount == 0)
            return [];

        var result = new Vector3[tess.ElementCount * 3];

        for (int i = 0; i < tess.ElementCount; i++)
        {
            int elementIndex = i * 3;
            result[elementIndex] = ToVector3(tess.Vertices[tess.Elements[elementIndex]].Position);
            result[elementIndex + 1] = ToVector3(tess.Vertices[tess.Elements[elementIndex + 1]].Position);
            result[elementIndex + 2] = ToVector3(tess.Vertices[tess.Elements[elementIndex + 2]].Position);
        }

        return result;

        static Vector3 ToVector3(Vec3 p) => new((float)p.X, (float)p.Y, 0f);
    }

    #endregion

    #region Coloring

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddDemarcateEdge(
        ref List<VertexPositionColor> vertices,
        Point point1,
        Point point2,
        Color color,
        float thickness)
    {
        Vector2 p1 = new(point1.X, point1.Y);
        Vector2 p2 = new(point2.X, point2.Y);

        Vector2 direction = p2 - p1;
        if (direction.LengthSquared() <= 0.000001f)
            return;

        direction.Normalize();

        Vector2 normal = new Vector2(-direction.Y, direction.X) * (thickness * 0.5f);
        Vector3 v1 = new(p1 - normal, 0f);
        Vector3 v2 = new(p1 + normal, 0f);
        Vector3 v3 = new(p2 + normal, 0f);
        Vector3 v4 = new(p2 - normal, 0f);

        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v2, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v4, color));
    }

    private unsafe void SetCellColor(uint cellId, Color color)
    {
        if (cellId >= (uint)_voronoiCellArray.Length)
            return;

        int index = (int)cellId;
        int count = _cellGeometryCounts[index];

        if (count == 0)
            return;

        int start = _cellGeometryOffsets[index];

        fixed (VertexPositionColor* outputPtr = _cellBatchVertices)
        {
            var dst = outputPtr + start;
            for (int i = 0; i < count; i++)
                dst[i].Color = color;
        }
    }

    #endregion
}