using System;
using System.Collections.Generic;
using SKSSL.Utilities.Voronoi;

public class Triangle
{
    public Point[] Vertices { get; } = new Point[3];

    public Point Circumcenter { get; private set; }

    public IEnumerable<Triangle> Neighbors
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

    public double RadiusSquared;

    public Triangle? Neighbor0;
    public Triangle? Neighbor1;
    public Triangle? Neighbor2;

    public Triangle(Point point1, Point point2, Point point3)
    {
        if (point1 == point2 || point1 == point3 || point2 == point3)
            throw new ArgumentException("Must be 3 distinct points");

        if (!IsCounterClockwise(point1, point2, point3))
        {
            Vertices[0] = point1;
            Vertices[1] = point3;
            Vertices[2] = point2;
        }
        else
        {
            Vertices[0] = point1;
            Vertices[1] = point2;
            Vertices[2] = point3;
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

        return dx * dx + dy * dy < RadiusSquared;
    }

    public Triangle? GetNeighbor(int edge)
    {
        return edge switch
        {
            0 => Neighbor0,
            1 => Neighbor1,
            2 => Neighbor2,
            _ => throw new ArgumentOutOfRangeException(nameof(edge))
        };
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
            default:
                throw new ArgumentOutOfRangeException(nameof(edge));
        }
    }

    public int IndexOfEdge(Point a, Point b)
    {
        if ((Vertices[0] == a && Vertices[1] == b) ||
            (Vertices[0] == b && Vertices[1] == a))
            return 0;

        if ((Vertices[1] == a && Vertices[2] == b) ||
            (Vertices[1] == b && Vertices[2] == a))
            return 1;

        if ((Vertices[2] == a && Vertices[0] == b) ||
            (Vertices[2] == b && Vertices[0] == a))
            return 2;

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

        if (div == 0)
            throw new DivideByZeroException();

        var center = new Point(aux1 / div, aux2 / div);

        Circumcenter = center;

        double dx = center.X - p0.X;
        double dy = center.Y - p0.Y;

        RadiusSquared = dx * dx + dy * dy;
    }

    private static bool IsCounterClockwise(
        Point point1,
        Point point2,
        Point point3)
    {
        return
            (point2.X - point1.X) * (point3.Y - point1.Y) -
            (point3.X - point1.X) * (point2.Y - point1.Y) > 0;
    }

    public int FindContainingEdge(Point point)
    {
        double c0 =
            (Vertices[1].X - Vertices[0].X) *
            (point.Y - Vertices[0].Y) -
            (Vertices[1].Y - Vertices[0].Y) *
            (point.X - Vertices[0].X);

        if (c0 < 0)
            return 0;

        double c1 =
            (Vertices[2].X - Vertices[1].X) *
            (point.Y - Vertices[1].Y) -
            (Vertices[2].Y - Vertices[1].Y) *
            (point.X - Vertices[1].X);

        if (c1 < 0)
            return 1;

        double c2 =
            (Vertices[0].X - Vertices[2].X) *
            (point.Y - Vertices[2].Y) -
            (Vertices[0].Y - Vertices[2].Y) *
            (point.X - Vertices[2].X);

        if (c2 < 0)
            return 2;

        return -1;
    }

    internal static Triangle FindContainingTriangle(Point point, Triangle start)
    {
        Triangle current = start;
        while (true)
        {
            int edge = current.FindContainingEdge(point);
            if (edge < 0)
                return current;

            Triangle? next = current.GetNeighbor(edge);
            if (next == null)
                return current;

            current = next;
        }
    }
    
    
}