using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clipper2Lib;
using LibTessDotNet.Double;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
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

    private void ClipCellsAgainstGaps(GapGeometry gaps, IEnumerable<VoronoiCell> cells)
    {
        foreach (VoronoiCell cell in cells)
        {
            if (cell.IsCulled || cell.Vertices.Count < 3)
            {
                cell.RenderPaths = [];
                cell.IsCulled = true;
                continue;
            }

            Path64 subject = ToPath(cell.Vertices);
            Paths64 result = Clipper.Difference([subject], gaps.Paths, FillRule.NonZero);

            cell.RenderPaths = result.Count == 0 ? null : result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Path64 ToPath(List<Point> polygon)
    {
        var path = new Path64(polygon.Count);
        foreach (Point point in polygon)
            path.Add(new Point64(point.X, point.Y));
        return path;
    }

    private static bool ClipRayAxis(
        float origin,
        float direction,
        float min,
        float max,
        ref float tMin,
        ref float tMax)
    {
        // Ray is parallel to this axis.
        if (Math.Abs(direction) < 0.000001f)
            return origin >= min && origin <= max;

        float t1 = (min - origin) / direction;
        float t2 = (max - origin) / direction;
        if (t1 > t2)
            (t1, t2) = (t2, t1);

        tMin = Math.Max(tMin, t1);
        tMax = Math.Min(tMax, t2);

        return tMin <= tMax;
    }

    private static List<Point> ClipPolygonToBounds(List<Point> polygon, int width, int height)
    {
        if (polygon.Count < 3)
            return [];

        var result = polygon;

        result = ClipPolygon(result, p => p.X >= 0, (a, b)
            => IntersectVertical(a, b, 0));

        result = ClipPolygon(result, p => p.X <= width, (a, b)
            => IntersectVertical(a, b, width));

        result = ClipPolygon(result, p => p.Y >= 0, (a, b)
            => IntersectHorizontal(a, b, 0));

        result = ClipPolygon(result, p => p.Y <= height, (a, b)
            => IntersectHorizontal(a, b, height));

        return result;
    }

    private static List<Point> ClipPolygon(
        List<Point> polygon,
        Func<Point, bool> inside,
        Func<Point, Point, Point> intersection)
    {
        if (polygon.Count == 0)
            return [];

        var result = new List<Point>();

        Point previous = polygon[^1];
        bool previousInside = inside(previous);

        foreach (Point current in polygon)
        {
            bool currentInside = inside(current);

            if (currentInside)
            {
                if (!previousInside)
                    result.Add(intersection(previous, current));

                result.Add(current);
            }
            else if (previousInside)
            {
                result.Add(intersection(previous, current));
            }

            previous = current;
            previousInside = currentInside;
        }

        return result.Distinct().ToList();
    }

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
        if (_boundaryMode == VoronoiBoundaryMode.CulledSquare &&
            _voronoiCells.TryGetValue(site, out VoronoiCell? cell) &&
            (cell.IsBoundary || cell.Vertices.Count == 0))
            return true;

        return false;
    }

    private bool ClipLineToBounds(Point p1, Point p2, out Point clipped1, out Point clipped2)
    {
        float x1 = p1.X;
        float y1 = p1.Y;
        float x2 = p2.X;
        float y2 = p2.Y;

        float dx = x2 - x1;
        float dy = y2 - y1;

        float t0 = 0f;
        float t1 = 1f;

        // Left: x >= 0
        if (!Clip(-dx, x1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Right: x <= width
        if (!Clip(dx, _width - x1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Top: y >= 0
        if (!Clip(-dy, y1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        // Bottom: y <= height
        if (!Clip(dy, _height - y1))
        {
            clipped1 = default;
            clipped2 = default;
            return false;
        }

        clipped1 = new Point((int)Math.Round(x1 + dx * t0), (int)Math.Round(y1 + dy * t0));
        clipped2 = new Point((int)Math.Round(x1 + dx * t1), (int)Math.Round(y1 + dy * t1));

        return true;

        bool Clip(float p, float q)
        {
            if (Math.Abs(p) < 0.000001f)
                return q >= 0f;

            float r = q / p;

            if (p < 0f)
            {
                if (r > t1)
                    return false;

                if (r > t0)
                    t0 = r;
            }
            else
            {
                if (r < t0)
                    return false;

                if (r < t1)
                    t1 = r;
            }

            return true;
        }
    }

    /// Map geometry is non-negative, so this is equivalent to Math.Round(value)
    /// for the normal coordinate range while avoiding the comparatively expensive
    /// Math.Round call.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long FastRoundToLong(double value) => (long)(value + 0.5);

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
        if (mode == VoronoiBoundaryMode.HardEdgeClosed)
        {
            // Artificial sites are triangulation scaffolding only.
            if (IsArtificialBoundarySite(a) || IsArtificialBoundarySite(b))
                return;

            // No neighbor means this isn't an internal Voronoi edge.
            // For HardEdgeClosed the screen itself closes the cell.
            if (neighbor == null)
                return;

            // An artificial neighboring triangle means this Delaunay
            // edge lies on the hull of the real points.
            //
            // Its Voronoi dual is an unbounded ray in the ordinary
            // Voronoi diagram, but HardEdgeClosed replaces that with
            // the screen boundary, so DO NOT draw it.
            if (IsArtificialBoundaryTriangle(neighbor))
                return;

            // Only emit each real-real Voronoi edge once.
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

        // Existing Culled / HardEdgeOpen behavior.
        if (neighbor != null)
        {
            if (triangle.Id >= neighbor.Id)
                return;

            if (mode == VoronoiBoundaryMode.CulledSquare && (ShouldCullBoundarySite(a) || ShouldCullBoundarySite(b)))
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

        if (!TryGetBoundaryVoronoiEdge(triangle, a, b, out Point start, out Point end))
            return;

        _voronoiEdges.Add(new Edge(start, end));
        _cellVoronoiEdges.Add(new CellVoronoiEdge(a, b, start, end));
    }

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
}