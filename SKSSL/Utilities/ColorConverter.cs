
using System.Drawing;
using RenderingLibrary.Graphics;
using Color = Microsoft.Xna.Framework.Color;

namespace SKSSL.Utilities;

public abstract class ColorConverter
{
    public static Color FromHex(string HEX) => ColorTranslator.FromHtml(HEX).ToXNA();
}