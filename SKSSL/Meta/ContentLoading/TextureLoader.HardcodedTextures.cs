using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

public partial class TextureLoader
{
    /// <summary>
    /// Programmer-assigned textures for use elsewhere.
    /// </summary>
    internal static class HardcodedTextures
    {
        private const int DEFAULT_WIDTH = 128;
        private const int DEFAULT_HEIGHT = 128;
        private static Texture2D? DefaultError;

        /// <returns>Cached Default Error Texture, or creates a new one if one is not present. Defaults to 128x128.</returns>
        public static Texture2D GetErrorTexture(int width = DEFAULT_WIDTH, int height = DEFAULT_HEIGHT)
        {
            if (DefaultError != null)
                return DefaultError;

            var tex = new Texture2D(SSLGame.Graphics, width, height);

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
}