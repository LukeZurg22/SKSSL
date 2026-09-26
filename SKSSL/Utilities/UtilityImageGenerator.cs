using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities;

// ReSharper disable once UnusedType.Global
public class UtilityImageGenerator
{
    // ReSharper disable once UnusedMember.Global
    public static Texture2D CreateCircularDensityMap(
        GraphicsDevice graphicsDevice,
        int width, int height,
        float radius = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(graphicsDevice);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(radius);

        var pixels = new Color[width * height];

        float centerX = (width - 1) * 0.5f;
        float centerY = (height - 1) * 0.5f;

        // Use the smaller dimension so the density remains circular.
        float maxRadius = MathF.Min(width, height) * 0.5f * radius;
        float inverseRadius = 1.0f / maxRadius;
        for (int y = 0; y < height; y++)
        {
            float dy = y - centerY;

            for (int x = 0; x < width; x++)
            {
                float dx = x - centerX;

                float distance = MathF.Sqrt(dx * dx + dy * dy);
                float normalized = distance * inverseRadius;

                // 1 at center -> 0 at radius.
                float density = 1.0f - MathF.Min(normalized, 1.0f);

                // Smooth the falloff.
                density = density * density * (3.0f - 2.0f * density);
                byte value = (byte)(density * 255.0f);
                pixels[y * width + x] = new Color(value, value, value, (byte)255);
            }
        }

        Texture2D texture = new(graphicsDevice, width, height, false, SurfaceFormat.Color);
        texture.SetData(pixels);
        return texture;
    }
}