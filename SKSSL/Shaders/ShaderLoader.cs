namespace SKSSL.Shaders;

using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;

public static class EffectLoader
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="graphicsDevice"></param>
    /// <param name="name">Must be "[name].mgfxo" format.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public static Effect LoadEmbedded(GraphicsDevice graphicsDevice, string name)
    {
        Assembly assembly = typeof(EffectLoader).Assembly;

        string resourceName = $"SKSSL.Shaders.{name}";
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
                              ?? throw new InvalidOperationException($"Embedded shader not found: {resourceName}");

        using var memory = new MemoryStream();
        stream.CopyTo(memory);

        return new Effect(graphicsDevice, memory.ToArray());
    }
}