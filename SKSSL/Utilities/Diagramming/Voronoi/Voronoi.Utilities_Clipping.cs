using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    /// <summary>
    /// Clips cell polygon against a boundary half-plane defined by normal (nx, ny) passing through (mx, my).
    /// </summary>
    private static List<DoublePoint> ClipHalfPlane(List<DoublePoint> polygon,
        double nx,
        double ny,
        double mx,
        double my)
    {
        if (polygon.Count == 0)
            return polygon;

        var output = new List<DoublePoint>(polygon.Count);
        for (int i = 0; i < polygon.Count; i++)
        {
            DoublePoint current = polygon[i];
            DoublePoint prev = polygon[(i + polygon.Count - 1) % polygon.Count];

            bool currentInside = (current.X - mx) * nx + (current.Y - my) * ny >= 0;
            bool prevInside = (prev.X - mx) * nx + (prev.Y - my) * ny >= 0;

            if (currentInside)
            {
                if (!prevInside) output.Add(Intersect(prev, current, nx, ny, mx, my));
                output.Add(current);
            }
            else if (prevInside) output.Add(Intersect(prev, current, nx, ny, mx, my));
        }

        return RemoveDuplicatePoints(output);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
}