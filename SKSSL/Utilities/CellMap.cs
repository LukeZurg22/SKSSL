using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace SKSSL.Utilities;

/// <summary>
/// Dynamic renderable cell map.
/// </summary>
public partial class CellMap
{
    private readonly Texture2D _pixelMap;
    private readonly Texture2D? _overlay;

    public CellMap(Texture2D pixelMap, Texture2D? overlay)
    {
        _pixelMap = pixelMap;
        _overlay = overlay;
    }

    public void Draw(SpriteBatch? spriteBatch)
    {
        if (spriteBatch == null)
            return;

        spriteBatch.Begin();
        spriteBatch.Draw(_pixelMap, Vector2.Zero, Color.White);
        if (_overlay != null)
            spriteBatch.Draw(_overlay, Vector2.Zero, Color.White);
        spriteBatch.End();
    }
}