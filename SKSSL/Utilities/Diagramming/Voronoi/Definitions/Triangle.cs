using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace SKSSL.Utilities.Voronoi;

public class Triangle
{
    public Point[] Vertices { get; } = new Point[3];
    public Point Circumcenter { get; private set; }

    private static int nextId;
    internal readonly int Id = nextId++;

    public Triangle? Neighbor0;
    public Triangle? Neighbor1;
    public Triangle? Neighbor2;

    // DelaunayTriangulator scratch state.
    internal bool Alive = true;
    internal int VisitStamp;
    internal int BadStamp;

    public Triangle(Point point1, Point point2, Point point3)
    {
        if (point1.ID == point2.ID || point1.ID == point3.ID || point2.ID == point3.ID)
        {
            throw new InvalidOperationException(
                $"Triangle contains duplicate Point IDs.\n" +
                $"P1: ID={point1.ID} ({point1.X}, {point1.Y})\n" +
                $"P2: ID={point2.ID} ({point2.X}, {point2.Y})\n" +
                $"P3: ID={point3.ID} ({point3.X}, {point3.Y})");
        }

        if (Cross(point1, point2, point3) < 0)
            (point2, point3) = (point3, point2);

        Vertices[0] = point1;
        Vertices[1] = point2;
        Vertices[2] = point3;

        Vertices[0].AdjacentTriangles.Add(this);
        Vertices[1].AdjacentTriangles.Add(this);
        Vertices[2].AdjacentTriangles.Add(this);

        UpdateCircumcircle();
    }

    private static double Cross(Point a, Point b, Point c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    public bool IsPointInsideCircumcircle(Point point)
    {
        double ax = (double)Vertices[0].X - point.X;
        double ay = (double)Vertices[0].Y - point.Y;

        double bx = (double)Vertices[1].X - point.X;
        double by = (double)Vertices[1].Y - point.Y;

        double cx = (double)Vertices[2].X - point.X;
        double cy = (double)Vertices[2].Y - point.Y;

        double a2 = ax * ax + ay * ay;
        double b2 = bx * bx + by * by;
        double c2 = cx * cx + cy * cy;

        double determinant =
            a2 * (bx * cy - by * cx) -
            b2 * (ax * cy - ay * cx) +
            c2 * (ax * by - ay * bx);

        return determinant > 0.0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetNeighbor(int edge, Triangle neighbor)
    {
        switch (edge)
        {
            case 0: Neighbor0 = neighbor; break;
            case 1: Neighbor1 = neighbor; break;
            case 2: Neighbor2 = neighbor; break;
            default: Debug.Fail("Invalid triangle edge."); break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int IndexOfEdge(Point a, Point b)
    {
        Point v0 = Vertices[0];
        Point v1 = Vertices[1];
        Point v2 = Vertices[2];

        if ((v0.ID == a.ID && v1.ID == b.ID) ||
            (v0.ID == b.ID && v1.ID == a.ID))
            return 0;

        if ((v1.ID == a.ID && v2.ID == b.ID) ||
            (v1.ID == b.ID && v2.ID == a.ID))
            return 1;

        if ((v2.ID == a.ID && v0.ID == b.ID) ||
            (v2.ID == b.ID && v0.ID == a.ID))
            return 2;

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateCircumcircle()
    {
        Point p0 = Vertices[0];
        Point p1 = Vertices[1];
        Point p2 = Vertices[2];

        double dA = (double)p0.X * p0.X + (double)p0.Y * p0.Y;
        double dB = (double)p1.X * p1.X + (double)p1.Y * p1.Y;
        double dC = (double)p2.X * p2.X + (double)p2.Y * p2.Y;
        double aux1 = dA * (p2.Y - p1.Y) + dB * (p0.Y - p2.Y) + dC * (p1.Y - p0.Y);
        double aux2 = -(dA * (p2.X - p1.X) + dB * (p0.X - p2.X) + dC * (p1.X - p0.X));
        double div = 2.0 * ((double)p0.X * (p2.Y - p1.Y) + (double)p1.X * (p0.Y - p2.Y) + (double)p2.X * (p1.Y - p0.Y));
        if (div == 0.0)
            throw new DivideByZeroException();

        Circumcenter = new Point((float)(aux1 / div), (float)(aux2 / div));
    }
}