using Microsoft.Xna.Framework;

namespace SKSSL.Extensions;

public static class VectorExtensions
{
    public static bool IsZero(this Color vector) => vector.PackedValue == 0;
    public static bool IsZero(this Vector4 vector) => vector.X == 0 && vector.Y == 0 && vector.Z == 0 && vector.W == 0;
    public static Color AsXnaColor(this Vector4 vector) => new(vector.X, vector.Y, vector.Z, vector.W);
    public static Color XnaColorFromName(string name)
    {
        System.Drawing.Color drawingColor = System.Drawing.Color.FromName(name);
        var xnaColor = new Color(drawingColor.R, drawingColor.G, drawingColor.B, drawingColor.A);
        return xnaColor;
    }
}