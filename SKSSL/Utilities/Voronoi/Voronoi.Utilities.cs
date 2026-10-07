using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clipper2Lib;
using LibTessDotNet.Double;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Mathematics;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private HilbertComparer? _hilbertComparer;

    private void AssignSpatialIds(List<Point> points)
    {
        int realCount = points.Count - 4;

        _firstArtificialId = (uint)realCount;
        _hilbertComparer ??= new HilbertComparer(_width, _height);

        points.Sort(4, realCount, _hilbertComparer);

        points[0] = new Point(points[0].X, points[0].Y, (uint)realCount);
        points[1] = new Point(points[1].X, points[1].Y, (uint)realCount + 1);
        points[2] = new Point(points[2].X, points[2].Y, (uint)realCount + 2);
        points[3] = new Point(points[3].X, points[3].Y, (uint)realCount + 3);

        for (int i = 0; i < realCount; ++i)
        {
            Point p = points[i + 4];
            points[i + 4] = new Point(p.X, p.Y, (uint)i);
        }
    }

    private static void SortVerticesAround(List<Point> vertices, Point center)
        => vertices.Sort(new VertexAngleComparer(center));

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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PointInPolygon(Path64 polygon, double x, double y)
    {
        bool inside = false;
        int count = polygon.Count;

        Point64 b = polygon[count - 1];
        for (int i = 0; i < count; ++i)
        {
            Point64 a = polygon[i];
            if (a.Y > y != b.Y > y)
            {
                double ax = a.X;
                double bx = b.X;
                if (x < (bx - ax) * (y - a.Y) / (b.Y - a.Y) + ax)
                    inside = !inside;
            }

            b = a;
        }

        return inside;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Crosses(double x, double y, Point a, Point b)
        => a.Y > y != b.Y > y && x < (b.X - a.X) * (y - a.Y) / (b.Y - a.Y) + a.X;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool PointOnSegment(double ax, double ay, double bx, double by, double px, double py)
    {
        double cross = (px - ax) * (by - ay) - (py - ay) * (bx - ax);
        if (Math.Abs(cross) > 0.000001)
            return false;
        return px >= Math.Min(ax, bx) && px <= Math.Max(ax, bx) && py >= Math.Min(ay, by) && py <= Math.Max(ay, by);
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint GetCellIdFromSiteId(uint siteId)
    {
        return siteId >= (uint)_cellsBySiteId.Length ? VoronoiCell.InvalidId : _cellsBySiteId[(int)siteId]!.ID;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point IntersectVertical(Point a, Point b, float x)
    {
        var dx = b.X - a.X;
        if (Math.Abs(dx) < 0.000001f)
            return new Point(x.FastRoundToInt(), a.Y);

        var t = (x - a.X) / dx;
        var y = a.Y + (b.Y - a.Y) * t;

        return new Point(x.FastRoundToInt(), y.FastRoundToInt());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Point IntersectHorizontal(Point a, Point b, float y)
    {
        var dy = b.Y - a.Y;
        if (Math.Abs(dy) < 0.000001f)
            return new Point(a.X, (int)Math.Round(y));

        var t = (y - a.Y) / dy;
        var x = a.X + (b.X - a.X) * t;
        return new Point(x.FastRoundToInt(), y.FastRoundToInt());
    }


    /// <remarks>Needed for debug in order to confirm nothings gone wrong.</remarks>
    [Conditional("DEBUG")]
    private static void ValidateNeighbors(List<Triangle> triangles)
    {
        foreach (Triangle t in triangles)
        {
            Validate(t, 0, t.Neighbor0);
            Validate(t, 1, t.Neighbor1);
            Validate(t, 2, t.Neighbor2);
        }

        return;

        static void Validate(Triangle t, int edge, Triangle? n)
        {
            if (n == null)
                return;

            Point a = t.Vertices[edge];
            Point b = t.Vertices[(edge + 1) % 3];

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
        if (site.ID >= _firstArtificialId)
            return true;

        if (_boundaryMode != VoronoiBoundaryMode.CulledSquare &&
            _boundaryMode != VoronoiBoundaryMode.CulledCircular)
            return false;

        VoronoiCell? cell = _cellsBySiteId[(int)site.ID];
        return cell != null && (cell.IsBoundary || cell.Vertices.Count == 0);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddVoronoiEdges(Triangle triangle, VoronoiBoundaryMode mode)
    {
        // Edge 0: vertices 0 -> 1
        AddVoronoiEdge(triangle, triangle.Neighbor0, triangle.Vertices[0], triangle.Vertices[1], mode);

        // Edge 1: vertices 1 -> 2
        AddVoronoiEdge(triangle, triangle.Neighbor1, triangle.Vertices[1], triangle.Vertices[2], mode);

        // Edge 2: vertices 2 -> 0
        AddVoronoiEdge(triangle, triangle.Neighbor2, triangle.Vertices[2], triangle.Vertices[0], mode);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        VoronoiCell? ca = _cellsBySiteId[(int)a.ID];
        VoronoiCell? cb = _cellsBySiteId[(int)b.ID];
        return ca != null && !ca.HasRenderableGeometry && cb != null && !cb.HasRenderableGeometry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool HasRenderableCellGeometry(VoronoiCell cell)
        => !cell.IsCulled && !IsArtificialBoundarySite(cell.Site) &&
           (cell.RenderPaths is not null ? cell.RenderPaths.Count != 0 : cell.Vertices.Count >= 3);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsArtificialBoundarySite(Point site) => site.ID >= _firstArtificialId;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsArtificialBoundaryTriangle(Triangle triangle)
    {
        uint first = _firstArtificialId;
        return triangle.Vertices[0].ID >= first || triangle.Vertices[1].ID >= first || triangle.Vertices[2].ID >= first;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetBoundaryVoronoiEdge(Triangle triangle, Point a, Point b, out Point start, out Point end)
    {
        float ex = b.X - a.X;
        float ey = b.Y - a.Y;

        float lenSq = ex * ex + ey * ey;

        if (lenSq <= 0.000001f)
        {
            start = default;
            end = default;
            return false;
        }

        float invLen = 1f / MathF.Sqrt(lenSq);

        float nx = -ey * invLen;
        float ny = ex * invLen;

        float mx = (a.X + b.X) * 0.5f;
        float my = (a.Y + b.Y) * 0.5f;

        Point thirdPoint;

        if (triangle.Vertices[0] != a && triangle.Vertices[0] != b)
            thirdPoint = triangle.Vertices[0];
        else if (triangle.Vertices[1] != a && triangle.Vertices[1] != b)
            thirdPoint = triangle.Vertices[1];
        else
            thirdPoint = triangle.Vertices[2];

        float tx = thirdPoint.X - mx;
        float ty = thirdPoint.Y - my;

        if (nx * tx + ny * ty > 0f)
        {
            nx = -nx;
            ny = -ny;
        }

        float ox = triangle.Circumcenter.X;
        float oy = triangle.Circumcenter.Y;

        float tMin = 0f;
        float tMax = float.MaxValue;

        if (!ClipRayAxis(ox, nx, 0f, _width, ref tMin, ref tMax) ||
            !ClipRayAxis(oy, ny, 0f, _height, ref tMin, ref tMax) ||
            tMax < tMin ||
            tMax < 0f)
        {
            start = default;
            end = default;
            return false;
        }

        if (tMin < 0f)
            tMin = 0f;

        float x1 = ox + nx * tMin;
        float y1 = oy + ny * tMin;
        float x2 = ox + nx * tMax;
        float y2 = oy + ny * tMax;

        if (x1 < 0f) x1 = 0f;
        else if (x1 > _width) x1 = _width;

        if (y1 < 0f) y1 = 0f;
        else if (y1 > _height) y1 = _height;

        if (x2 < 0f) x2 = 0f;
        else if (x2 > _width) x2 = _width;

        if (y2 < 0f) y2 = 0f;
        else if (y2 > _height) y2 = _height;

        start = new Point(x1.FastRoundToInt(), y1.FastRoundToInt());
        end = new Point(x2.FastRoundToInt(), y2.FastRoundToInt());
        return start != end;
    }

    #region TESSELATION

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3[] TessellateOriginalCellPositions(List<Point> vertices)
    {
        if (vertices.Count < 3)
            return [];

        var result = new Vector3[(vertices.Count - 2) * 3];
        ReadOnlySpan<Point> points = CollectionsMarshal.AsSpan(vertices);
        Point origin = points[0];

        int index = 0;
        for (int i = 1; i < points.Length - 1; ++i)
        {
            Point b = points[i];
            Point c = points[i + 1];

            float area = (b.X - origin.X) * (c.Y - origin.Y) - (b.Y - origin.Y) * (c.X - origin.X);
            if (MathF.Abs(area) <= 0.0001f)
                continue;

            result[index++] = new Vector3(origin.X, origin.Y, 0f);
            result[index++] = new Vector3(b.X, b.Y, 0f);
            result[index++] = new Vector3(c.X, c.Y, 0f);
        }

        if (index == result.Length)
            return result;

        Array.Resize(ref result, index);
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector3[] TessellateClippedCellPositions(Paths64 paths)
    {
        if (paths.Count == 0)
            return [];

        Tess tess = new();
        foreach (Path64 path in paths)
        {
            int count = path.Count;
            if (count < 3)
                continue;

            var contour = new ContourVertex[count];
            for (int i = 0; i < count; ++i)
            {
                Point64 p = path[i];
                contour[i].Position = new Vec3(p.X, p.Y, 0.0);
            }

            tess.AddContour(contour);
        }

        tess.Tessellate();

        int elementCount = tess.ElementCount;
        if (elementCount == 0)
            return [];

        var result = new Vector3[elementCount * 3];

        var vertices = tess.Vertices;
        int[] elements = tess.Elements;

        for (int i = 0, j = 0; i < elementCount; ++i)
        {
            int e = elements[j++];
            Vec3 p = vertices[e].Position;
            result[j - 1] = new Vector3((float)p.X, (float)p.Y, 0f);

            e = elements[j++];
            p = vertices[e].Position;
            result[j - 1] = new Vector3((float)p.X, (float)p.Y, 0f);

            e = elements[j++];
            p = vertices[e].Position;
            result[j - 1] = new Vector3((float)p.X, (float)p.Y, 0f);
        }

        return result;
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
        // Vector2's, but as floats.
        float dx = point2.X - point1.X;
        float dy = point2.Y - point1.Y;

        // Direction.
        float lenSq = dx * dx + dy * dy;
        if (lenSq <= 0.000001f)
            return;

        // Normalize.
        float invLen = thickness * 0.5f / MathF.Sqrt(lenSq);

        // Normals
        float nx = -dy * invLen;
        float ny = dx * invLen;

        // Points
        float x1 = point1.X;
        float y1 = point1.Y;
        float x2 = point2.X;
        float y2 = point2.Y;

        Vector3 v1 = new(x1 - nx, y1 - ny, 0f);
        Vector3 v2 = new(x1 + nx, y1 + ny, 0f);
        Vector3 v3 = new(x2 + nx, y2 + ny, 0f);
        Vector3 v4 = new(x2 - nx, y2 - ny, 0f);

        // TODO: This .Add() method may add overhead, and could be optimized away.
        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v2, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v1, color));
        vertices.Add(new VertexPositionColor(v3, color));
        vertices.Add(new VertexPositionColor(v4, color));
    }

    #endregion
}