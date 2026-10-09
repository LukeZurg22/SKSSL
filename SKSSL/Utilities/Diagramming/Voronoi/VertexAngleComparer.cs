using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

internal readonly struct VertexAngleComparer : IComparer<Point>
{
    private readonly Point _centre;
    public VertexAngleComparer(Point center) => _centre = center;

    public int Compare(Point x, Point y)
    {
        float ax = x.X - _centre.X;
        float ay = x.Y - _centre.Y;
        float bx = y.X - _centre.X;
        float by = y.Y - _centre.Y;
        bool aUpper = ay > 0 || (ay == 0 && ax >= 0);
        bool bUpper = by > 0 || (by == 0 && bx >= 0);
        if (aUpper != bUpper) return aUpper ? -1 : 1;
        float cross = ax * by - ay * bx;
        return cross > 0 ? -1 : cross < 0 ? 1 : 0;
    }
}