
// ReSharper disable UnusedType.Global

using System.Numerics;

namespace SKSSL.Types;

public class Box<T> where T : INumber<T>
{
    private T Value;

    public Box(T value) => Value = value;

    /// Prefix increment
    public static Box<T> operator ++(Box<T> box)
    {
        box.Value += T.One;
        return box;
    }

    public override string ToString() => Value.ToString()!;
}