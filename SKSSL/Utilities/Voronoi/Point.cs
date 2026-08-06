using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

public readonly struct Point : IEquatable<Point>
{
    /// <summary>
    /// Used only for generating a unique ID for each instance of this class that gets generated
    /// </summary>
    private static int _counter;

    /// <summary>
    /// Used for identifying an instance of a class; can be useful in troubleshooting when geometry goes weird
    /// (e.g. when trying to identify when Triangle objects are being created with the same Point object twice)
    /// </summary>
    private readonly int _instanceId = _counter++;

    public double X { get; }
    public double Y { get; }
    public HashSet<Triangle> AdjacentTriangles { get; } = [];

    public Point(double x, double y)
    {
        X = x;
        Y = y;
    }

    // Simple way of seeing what's going on in the debugger when investigating unexpected behaviour.
    public override string ToString() =>
        $"{nameof(Point)} {_instanceId} {X:0.##}@{Y:0.##}";

    public bool Equals(Point other)
    {
        return _instanceId == other._instanceId && X.Equals(other.X) && Y.Equals(other.Y) &&
               Equals(AdjacentTriangles, other.AdjacentTriangles);
    }

    public override bool Equals(object obj) => obj is Point other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(_instanceId, X, Y, AdjacentTriangles);

    public static bool operator ==(Point left, Point right) => left.Equals(right);

    public static bool operator !=(Point left, Point right) => !(left == right);
}