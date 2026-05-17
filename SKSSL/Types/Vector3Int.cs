using System.Numerics;

namespace SKSSL.Types;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
/// <summary>
/// A vector of three integer values: (<see cref="X"/>, <see cref="Y"/> <see cref="Z"/>)
/// </summary>
public readonly struct Vector3Int : IEquatable<Vector3Int>
{
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    /// <summary>
    /// Instantiates an instance of <see cref="Vector3Int"/> with all values 0.
    /// </summary>
    public Vector3Int()
    {
        X = 0;
        Y = 0;
        Z = 0;
    }

    /// <summary>
    /// Instantiates an instance of <see cref="Vector3Int"/> with all values equal to the provided value.
    /// </summary>
    public Vector3Int(int val)
    {
        X = val;
        Y = val;
        Z = val;
    }

    /// <summary>
    /// Instantiates an instance of <see cref="Vector3Int"/> with x and y equal to the provided values, with z set 0.
    /// </summary>
    public Vector3Int(int x, int y)
    {
        X = x;
        Y = y;
        Z = 0;
    }

    /// <summary>
    /// Instantiates an instance of <see cref="Vector3Int"/> with provided x, y, and z values.
    /// </summary>
    public Vector3Int(int x, int y, int z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    #region Operator Overloads

    public static Vector3Int operator +(Vector3Int a, Vector3Int b) =>
        new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vector3Int operator -(Vector3Int a, Vector3Int b) =>
        new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static bool operator ==(Vector3Int a, Vector3Int b) =>
        a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    public static bool operator !=(Vector3Int a, Vector3Int b) => !(a == b);

    #endregion

    #region Handy Methods

    public override bool Equals(object? obj) => obj is Vector3Int v && this == v;
    public bool Equals(Vector3Int other) => X == other.X && Y == other.Y && Z == other.Z;

    public override int GetHashCode() => HashCode.Combine(X, Y, Z);
    public override string ToString() => $"({X}, {Y}, {Z})";

    public float Length() => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public int ManhattanDistance(Vector3Int other) =>
        Math.Abs(X - other.X) + Math.Abs(Y - other.Y) + Math.Abs(Z - other.Z);

    /// <returns>
    /// If this <see cref="Vector3Int"/>'s values all equal 0.
    /// </returns>
    public bool IsZero => X == 0 && Y == 0 && Z == 0;

    public Vector3 ToVector3() => new(X, Y, Z);
    public Vector3 Center() => new(X + 0.5f, Y + 0.5f, Z + 0.5f);
    public static Vector3Int Zero() => new(0, 0, 0);

    #endregion
}