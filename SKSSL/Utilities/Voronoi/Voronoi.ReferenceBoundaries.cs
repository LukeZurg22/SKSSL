using System;
using System.Runtime.CompilerServices;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static DoublePoint Intersect(DoublePoint p1, DoublePoint p2,
        double nx, double ny, double mx, double my)
    {
        double dx = p2.X - p1.X;
        double dy = p2.Y - p1.Y;

        double denominator = dx * nx + dy * ny;
        if (Math.Abs(denominator) < 1e-9)
            return p1;

        double t = ((mx - p1.X) * nx + (my - p1.Y) * ny) / denominator;
        t = Math.Clamp(t, 0.0, 1.0);

        return new DoublePoint(p1.X + t * dx, p1.Y + t * dy);
    }
}