using System;
using System.Collections.Generic;
using System.Linq;
using Clipper2Lib;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private static List<DoublePoint> ClipHalfPlane(List<DoublePoint> polygon, double a, double b, double c)
    {
        if (polygon.Count == 0)
            return [];

        var result = new List<DoublePoint>(polygon.Count + 2);

        DoublePoint previous = polygon[^1];
        double previousValue =
            a * previous.X +
            b * previous.Y -
            c;

        bool previousInside = previousValue <= 0.0;

        foreach (DoublePoint current in polygon)
        {
            double currentValue = a * current.X + b * current.Y - c;
            bool currentInside = currentValue <= 0.0;
            if (currentInside)
            {
                if (!previousInside)
                {
                    double denominator = previousValue - currentValue;

                    if (Math.Abs(denominator) > 1e-12)
                    {
                        double t = previousValue / denominator;

                        result.Add(new DoublePoint(
                            previous.X + (current.X - previous.X) * t,
                            previous.Y + (current.Y - previous.Y) * t));
                    }
                }

                result.Add(current);
            }
            else if (previousInside)
            {
                double denominator = previousValue - currentValue;

                if (Math.Abs(denominator) > 1e-12)
                {
                    double t = previousValue / denominator;

                    result.Add(new DoublePoint(
                        previous.X + (current.X - previous.X) * t,
                        previous.Y + (current.Y - previous.Y) * t));
                }
            }

            previous = current;
            previousValue = currentValue;
            previousInside = currentInside;
        }

        return RemoveDuplicatePoints(result);
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

    private void ClipVoronoiEdgesAgainstGaps(GapGeometry gaps)
    {
        if (_voronoiEdges.Count == 0)
            return;

        int edgeCount = _voronoiEdges.Count;

        var subjects = new Paths64(edgeCount);

        for (int i = 0; i < edgeCount; i++)
        {
            Edge edge = _voronoiEdges[i];

            subjects.Add(
            [
                new Point64(edge.Point1.X, edge.Point1.Y),
                new Point64(edge.Point2.X, edge.Point2.Y)
            ]);
        }

        var clipped = new Paths64();
        var openClipped = new Paths64();

        var clipper = new Clipper64();

        clipper.AddOpenSubject(subjects);
        clipper.AddClip(gaps.Paths);
        clipper.Execute(ClipType.Difference, FillRule.EvenOdd, clipped, openClipped);

        _voronoiEdges.Clear();

        foreach (Path64 path in openClipped)
        {
            if (path.Count < 2)
                continue;

            for (int i = 1; i < path.Count; i++)
            {
                Point64 a = path[i - 1];
                Point64 b = path[i];

                if (a == b)
                    continue;

                _voronoiEdges.Add(new Edge(new Point((int)a.X, (int)a.Y), new Point((int)b.X, (int)b.Y)));
            }
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