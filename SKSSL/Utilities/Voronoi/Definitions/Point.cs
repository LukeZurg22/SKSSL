using System;
using System.Collections.Generic;

namespace SKSSL.Utilities.Voronoi;

// Heavens forbid one use this class outside of the Voronoi namespace.
public readonly struct Point : IEquatable<Point>
{
    /// <summary>
    /// Used only for generating a unique ID for each instance of this class that gets generated.
    /// </summary>
    /// <remarks>
    /// Generating an immense number of points over the integer limit will cause a crash. Who would do this?
    /// I don't know. I -do- know that this counter is never reset.
    /// </remarks>
    private static uint _pointCounter = 0;

    /// <summary>
    /// Used for identifying an instance of a class; can be useful in troubleshooting when geometry goes weird
    /// (e.g. when trying to identify when Triangle objects are being created with the same Point object twice)
    /// </summary>
    public readonly uint ID = _pointCounter++;
    
    public float X { init; get; }
    public float Y { init; get; }
    public HashSet<Triangle> AdjacentTriangles { get; } = [];

    // ReSharper disable once UnusedMember.Global
    public Point(float x, float y, uint id)
    {
        X = x;
        Y = y;
        ID = id;
    }

    public Point(float x, float y)
    {
        X = x;
        Y = y;
    }

    // Simple way of seeing what's going on in the debugger when investigating unexpected behaviour.
    public override string ToString() => $"{nameof(Point)} {ID} {X:0.##}@{Y:0.##}";
    public static bool operator ==(Point left, Point right) => left.Equals(right);
    public static bool operator !=(Point left, Point right) => !(left == right);
    public bool Equals(Point other) => ID == other.ID;
    public override bool Equals(object? obj) => obj is Point other && Equals(other);
    public override int GetHashCode() => (int)ID;
}