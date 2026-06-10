using System.Diagnostics.Contracts;
using Microsoft.Xna.Framework;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Extensions;

public static class VectorExtensions
{
    [Pure]
    public static bool IsZero(this Color vector) => vector.PackedValue == 0;

    [Pure]
    public static bool IsZero(this Vector4 vector) => vector.X == 0 && vector.Y == 0 && vector.Z == 0 && vector.W == 0;

    [Pure]
    public static Color AsXnaColor(this Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);

    [Pure]
    public static Color XnaColorFromName(string name)
    {
        System.Drawing.Color drawingColor = System.Drawing.Color.FromName(name);
        var xnaColor = new Color(drawingColor.R, drawingColor.G, drawingColor.B, drawingColor.A);
        return xnaColor;
    }
}