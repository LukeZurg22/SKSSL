using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// ReSharper disable All

namespace SKSSL.Utilities;

public static class HardcodedAssets
{
    private static GraphicsDevice _graphicsDevice = null!;
    public static void Initialize(GraphicsDevice graphicsDevice) => _graphicsDevice = graphicsDevice;

    private static Texture2D? DefaultError;
    
    /// <returns>Cached Default Error Texture, or creates a new one if one is not present. Defaults to 128x128.</returns>
    public static Texture2D GetErrorTexture(int width = 128, int height = 128)
    {
        if (DefaultError != null)
            return DefaultError;
        
        var tex = new Texture2D(_graphicsDevice, width, height);

        var pixels = new Color[128 * 128];

        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            bool checker = (x / 32 + y / 32) % 2 == 0;
            pixels[y * 128 + x] = checker ? new Color(1f, 0f, 1f, 1f) : Color.Black; // Magenta / Black
        }

        tex.SetData(pixels);
        DefaultError = tex;
        return tex;
    }
}