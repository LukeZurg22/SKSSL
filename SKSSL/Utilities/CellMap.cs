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
    private readonly BasicEffect _effect;
    private readonly GraphicsDevice _graphicsDevice;

    public CellMap(GraphicsDevice graphicsDevice, Texture2D pixelMap, Texture2D? overlay)
    {
        _pixelMap = pixelMap;
        _overlay = overlay;
        _graphicsDevice = graphicsDevice;
        _effect = new BasicEffect(graphicsDevice)
        {
            VertexColorEnabled = true,
            TextureEnabled = false,
        };
    }


    // ReSharper disable once MemberCanBePrivate.Global
    public void SetDiagramProjection(Matrix world, Matrix view, int? width = null, int? height = null)
    {
        width ??= _pixelMap.Width;
        height ??= _pixelMap.Height;
        
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = Matrix
            .CreateOrthographicOffCenter(0f, width.Value, height.Value, 0f, 0f, 1f);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void SetScreenProjection(Matrix world, Matrix view)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _effect.World = world;
        _effect.View = view;
        _effect.Projection = Matrix
            .CreateOrthographicOffCenter(0f, viewport.Width, viewport.Height, 0f, 0f, 1f);
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