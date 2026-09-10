using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

public class Triangle
{
    public Point[] Vertices { get; } = new Point[3];
    public Point Circumcenter { get; private set; }

    private static int nextId;
    internal readonly int Id = nextId++;

    public IEnumerable<Triangle?> Neighbors
    {
        get
        {
            if (Neighbor0 != null)
                yield return Neighbor0;

            if (Neighbor1 != null)
                yield return Neighbor1;

            if (Neighbor2 != null)
                yield return Neighbor2;
        }
    }

    private double _radiusSquared;
    public Triangle? Neighbor0;
    public Triangle? Neighbor1;
    public Triangle? Neighbor2;

    // DelaunayTriangulator scratch state.
    internal bool Alive = true;
    internal int VisitStamp;
    internal int BadStamp;

    public Triangle(Point point1, Point point2, Point point3)
    {
        if (point1 == point2 || point1 == point3 || point2 == point3)
            throw new ArgumentException("Must be 3 distinct points");

        bool IsCounterClockwise =
            (point2.X - point1.X) * (point3.Y - point1.Y) -
            (point3.X - point1.X) * (point2.Y - point1.Y) > 0;

        Vertices[0] = point1;
        switch (IsCounterClockwise)
        {
            case false:
                Vertices[1] = point3;
                Vertices[2] = point2;
                break;
            default:
                Vertices[1] = point2;
                Vertices[2] = point3;
                break;
        }

        Vertices[0].AdjacentTriangles.Add(this);
        Vertices[1].AdjacentTriangles.Add(this);
        Vertices[2].AdjacentTriangles.Add(this);

        UpdateCircumcircle();
    }

    public bool IsPointInsideCircumcircle(Point point)
    {
        double dx = point.X - Circumcenter.X;
        double dy = point.Y - Circumcenter.Y;
        return dx * dx + dy * dy < _radiusSquared;
    }

    public void SetNeighbor(int edge, Triangle triangle)
    {
        switch (edge)
        {
            case 0:
                Neighbor0 = triangle;
                break;
            case 1:
                Neighbor1 = triangle;
                break;
            case 2:
                Neighbor2 = triangle;
                break;
            default: throw new ArgumentOutOfRangeException(nameof(edge));
        }
    }

    public int IndexOfEdge(Point a, Point b)
    {
        Point v0 = Vertices[0];
        Point v1 = Vertices[1];
        Point v2 = Vertices[2];

        if ((v0 == a && v1 == b) || (v0 == b && v1 == a)) return 0;
        if ((v1 == a && v2 == b) || (v1 == b && v2 == a)) return 1;
        if ((v2 == a && v0 == b) || (v2 == b && v0 == a)) return 2;

        return -1;
    }

    private void UpdateCircumcircle()
    {
        Point p0 = Vertices[0];
        Point p1 = Vertices[1];
        Point p2 = Vertices[2];

        double dA = p0.X * p0.X + p0.Y * p0.Y;
        double dB = p1.X * p1.X + p1.Y * p1.Y;
        double dC = p2.X * p2.X + p2.Y * p2.Y;

        double aux1 =
            dA * (p2.Y - p1.Y) +
            dB * (p0.Y - p2.Y) +
            dC * (p1.Y - p0.Y);

        double aux2 =
            -(dA * (p2.X - p1.X) +
              dB * (p0.X - p2.X) +
              dC * (p1.X - p0.X));

        double div =
            2 * (
                p0.X * (p2.Y - p1.Y) +
                p1.X * (p0.Y - p2.Y) +
                p2.X * (p1.Y - p0.Y));

        if (div == 0) throw new DivideByZeroException();

        var center = new Point(aux1 / div, aux2 / div);
        Circumcenter = center;

        double dx = center.X - p0.X;
        double dy = center.Y - p0.Y;
        _radiusSquared = dx * dx + dy * dy;
    }
}