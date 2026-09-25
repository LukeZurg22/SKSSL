using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Clipper2Lib;
using LibTessDotNet.Double;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    internal static void SortVerticesAround(List<Point> vertices, Point center)
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

    internal static bool IsPointInsideCell(VoronoiCell cell, int x, int y)
    {
        /*
         * If the cell has been gap-clipped, test the clipped
         * geometry instead of the original Voronoi polygon.
         */
        if (cell.RenderPaths is not { Count: > 0 } paths)
            return cell.Vertices.Count >= 3 && PointInPolygon(cell.Vertices, x, y);

        bool inside = false;
        foreach (Path64 path in paths)
        {
            if (path.Count < 3)
                continue;

            if (PointInPolygon(path, x, y))
                inside = !inside;
        }

        return inside;
    }

    private static bool PointInPolygon(List<Point> polygon, double x, double y)
    {
        bool inside = false;

        int count = polygon.Count;

        for (int i = 0, j = count - 1;
             i < count;
             j = i++)
        {
            Point a = polygon[i];
            Point b = polygon[j];

            /*
             * Boundary test first so clicking directly on an edge
             * still counts as being inside the cell.
             */
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

    internal static void ClipCellsAgainstGaps(GapGeometry gaps, IEnumerable<VoronoiCell> cells)
    {
        foreach (VoronoiCell cell in cells)
        {
            cell.RenderPaths = null;

            if (cell.Vertices.Count < 3)
            {
                cell.RenderPaths = [];
                continue;
            }

            if (gaps.Paths.Count == 0)
                continue;

            Path64 subject = ToPath(cell.Vertices);
            Paths64 result = Clipper.Difference([subject], gaps.Paths, FillRule.NonZero);
            cell.RenderPaths = result;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Path64 ToPath(List<Point> polygon)
    {
        var path = new Path64(polygon.Count);
        // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
        foreach (Point point in polygon)
            path.Add(new Point64(point.X, point.Y));
        return path;
    }
    
    internal static bool ClipRayAxis(
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

    internal static List<Point> ClipPolygonToBounds(List<Point> polygon, int width, int height)
    {
        if (polygon.Count < 3)
            return [];

        var result = polygon;

        result = ClipPolygon(
            result,
            p => p.X >= 0,
            (a, b) => IntersectVertical(a, b, 0));

        result = ClipPolygon(
            result,
            p => p.X <= width,
            (a, b) => IntersectVertical(a, b, width));

        result = ClipPolygon(
            result,
            p => p.Y >= 0,
            (a, b) => IntersectHorizontal(a, b, 0));

        result = ClipPolygon(
            result,
            p => p.Y <= height,
            (a, b) => IntersectHorizontal(a, b, height));

        return result;
    }

    internal static List<Point> ClipPolygon(
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

    private static Point IntersectVertical(
        Point a,
        Point b,
        float x)
    {
        var dx = b.X - a.X;

        if (Math.Abs(dx) < 0.000001f)
            return new Point(
                (int)Math.Round(x),
                a.Y);

        var t = (x - a.X) / dx;
        var y = a.Y + (b.Y - a.Y) * t;

        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }

    internal static Point IntersectHorizontal(Point a, Point b, float y)
    {
        var dy = b.Y - a.Y;
        if (Math.Abs(dy) < 0.000001f)
            return new Point(a.X, (int)Math.Round(y));

        var t = (y - a.Y) / dy;
        var x = a.X + (b.X - a.X) * t;

        return new Point((int)Math.Round(x), (int)Math.Round(y));
    }


    internal static VertexPositionColor[] TessellateClippedCell(Paths64 paths, Color color)
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
                contour[i].Data = color;
            }

            tess.AddContour(contour);
        }

        // Tessellate BEFORE checking VertexCount/ElementCount.
        tess.Tessellate();

        if (tess.ElementCount == 0)
            return [];

        var result =
            new VertexPositionColor[tess.ElementCount * 3];

        for (int i = 0; i < tess.ElementCount; i++)
        {
            int elementIndex = i * 3;

            for (int j = 0; j < 3; j++)
            {
                int vertexIndex = tess.Elements[elementIndex + j];

                Vec3 position = tess.Vertices[vertexIndex].Position;

                result[elementIndex + j] =
                    new VertexPositionColor(
                        new Vector3(
                            (float)position.X,
                            (float)position.Y,
                            0f),
                        color);
            }
        }

        return result;
    }

    private static VertexPositionColor[] TessellateOriginalCell(List<Point> vertices, Color color)
    {
        if (vertices.Count < 3)
            return [];

        var result =
            new VertexPositionColor[
                (vertices.Count - 2) * 3];

        Vector3 origin =
            new(vertices[0].X, vertices[0].Y, 0f);

        int index = 0;

        for (int i = 1; i < vertices.Count - 1; i++)
        {
            result[index++] =
                new VertexPositionColor(
                    origin,
                    color);

            result[index++] =
                new VertexPositionColor(
                    new Vector3(
                        vertices[i].X,
                        vertices[i].Y,
                        0f),
                    color);

            result[index++] =
                new VertexPositionColor(
                    new Vector3(
                        vertices[i + 1].X,
                        vertices[i + 1].Y,
                        0f),
                    color);
        }

        return result;
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
}