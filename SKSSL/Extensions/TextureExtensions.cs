using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Textures;

public static class TextureExtensions
{
    /// <summary>
    /// Creates a new Texture2D with full mipmaps from an existing Texture2D.
    /// The original texture is unchanged.
    /// </summary>
    public static Texture2D ToMipMapped(this Texture2D source)
    {
        ArgumentNullException.ThrowIfNull(source);
        GraphicsDevice graphicsDevice = source.GraphicsDevice;

        // Create a new texture that supports mipmaps
        using var renderTarget = new RenderTarget2D(
            graphicsDevice,
            source.Width,
            source.Height,
            mipMap: true,
            preferredFormat: source.Format,
            preferredDepthFormat: DepthFormat.None,
            preferredMultiSampleCount: 0,
            usage: RenderTargetUsage.DiscardContents);

        // Draw the original texture into the render target (level 0)
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(Color.Transparent);

        using var spriteBatch = new SpriteBatch(graphicsDevice);
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Opaque, SamplerState.PointClamp);
        spriteBatch.Draw(source, Vector2.Zero, Color.White);
        spriteBatch.End();

        graphicsDevice.SetRenderTarget(null);

        // Create final mip-mapped texture and copy all levels.
        var mipMappedTexture = new Texture2D(
            graphicsDevice,
            source.Width,
            source.Height,
            mipmap: true,
            format: source.Format);

        // Copy each mipmap level from the render target.
        for (int level = 0; level < renderTarget.LevelCount; level++)
        {
            int mipWidth = Math.Max(1, source.Width >> level);
            int mipHeight = Math.Max(1, source.Height >> level);
            int elementCount = mipWidth * mipHeight;

            // Color[] for simplicity.
            var data = new Color[elementCount];
            renderTarget.GetData(level, null, data, 0, elementCount);
            mipMappedTexture.SetData(level, null, data, 0, elementCount);
        }

        source.Dispose();
        return mipMappedTexture;
    }
}