using System.Diagnostics.CodeAnalysis;

namespace SKSSL.Extensions;

[SuppressMessage("ReSharper", "RedundantNameQualifier")]
public static class StructExtensions
{
    // public static T Clone<T> ( this T val ) where T : struct => val;
    public static T Clone<T>(this T val) where T : struct => val;

    public static Microsoft.Xna.Framework.Color FromName(this Microsoft.Xna.Framework.Color color, string colorName)
    {
        System.Drawing.Color drawingColor = System.Drawing.Color.FromName(colorName);
        var xnaColor = new Microsoft.Xna.Framework.Color(drawingColor.R, drawingColor.G, drawingColor.B, drawingColor.A);
        return xnaColor;
    }

}