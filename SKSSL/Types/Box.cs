
// ReSharper disable UnusedType.Global

namespace SKSSL.Types;

public class Box<T> where T : struct
{
    private T Value;

    public Box(T value)
    {
        Value = value;
    }

    // Prefix increment
    public static Box<T> operator ++(Box<T> box)
    {
        box.Value = box.Value switch
        {
            int i => (T)(object)(i + 1),
            long l => (T)(object)(l + 1),
            float f => (T)(object)(f + 1),
            double d => (T)(object)(d + 1),
            _ => throw new InvalidOperationException("Unsupported numeric type.")
        };
        return box;
    }

    public override string ToString() => Value.ToString()!;
}