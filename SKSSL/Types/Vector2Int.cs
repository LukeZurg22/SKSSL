using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;

namespace SKSSL.Types;

/// <summary>
/// A vector of three integer values: (<see cref="X"/>, <see cref="Y"/>)
/// </summary>
public readonly struct Vector2Int : IEquatable<Vector2Int>
{
    public readonly int X;
    public readonly int Y;

    /// <summary>
    /// Instantiates an instance of <see cref="Vector2Int"/> with all values 0.
    /// </summary>
    public Vector2Int()
    {
        X = 0;
        Y = 0;
    }

    /// <summary>
    /// Instantiates an instance of <see cref="Vector2Int"/> with x and y equal to the provided values, or 0.
    /// </summary>
    public Vector2Int(int x = 0, int y = 0)
    {
        X = x;
        Y = y;
    }


    #region Operator Overloads

    public static Vector2Int operator +(Vector2Int a, Vector2Int b) =>
        new(a.X + b.X, a.Y + b.Y);

    public static Vector2Int operator -(Vector2Int a, Vector2Int b) =>
        new(a.X - b.X, a.Y - b.Y);

    public static bool operator ==(Vector2Int a, Vector2Int b) =>
        a.X == b.X && a.Y == b.Y;

    public static bool operator !=(Vector2Int a, Vector2Int b) => !(a == b);

    #endregion

    #region Handy Methods

    public override bool Equals(object? obj) => obj is Vector2Int v && this == v;
    public bool Equals(Vector2Int other) => X == other.X && Y == other.Y;

    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public float Length() => MathF.Sqrt(X * X + Y * Y);

    /// <returns>
    /// If this <see cref="Vector2Int"/>'s values all equal 0.
    /// </returns>
    public bool IsZero => X == 0 && Y == 0;

    public Vector2 ToVector2() => new(X, Y);
    public Vector2 Center() => new(X + 0.5f, Y + 0.5f);

    [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
    public static Vector2Int Zero() => new(0, 0);

    #endregion
}