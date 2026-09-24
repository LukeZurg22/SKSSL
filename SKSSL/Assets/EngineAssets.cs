using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Assets;

/// <summary>
/// Content manager for loading MonoGame game assets from project-embedded resources.
/// </summary>
public sealed class EmbeddedContentManager : ContentManager
{
    private readonly Assembly _assembly;
    private readonly string _resourcePrefix;

    /// <summary>
    /// Base-most constructor just above the base-game content manager inherit.
    /// </summary>
    public EmbeddedContentManager(
        SSLGame game,
        Assembly assembly,
        string resourcePrefix = "SKSSL") : base(game.Content.ServiceProvider)
    {
        _assembly = assembly;
        _resourcePrefix = resourcePrefix;
    }

    public EmbeddedContentManager(SSLGame game, string resourcePrefix = "SKSSL")
        : this(game, typeof(EmbeddedContentManager).Assembly, resourcePrefix)
    {
    }

    public Texture2D LoadEmbeddedTexture(GraphicsDevice graphicsDevice, string resourceName)
    {
        string? manifestName = _assembly
            .GetManifestResourceNames()
            .FirstOrDefault(x =>
                x.Equals(resourceName, StringComparison.OrdinalIgnoreCase) ||
                x.EndsWith($".{resourceName}", StringComparison.OrdinalIgnoreCase));

        if (manifestName == null)
        {
            throw new FileNotFoundException(
                $"Embedded resource '{resourceName}' was not found in '{_assembly.GetName().Name}'.");
        }

        using Stream stream = _assembly.GetManifestResourceStream(manifestName)!;
        return Texture2D.FromStream(graphicsDevice, stream);
    }

    [Pure]
    protected override Stream OpenStream(string assetName)
        => _assembly.GetManifestResourceStream($"{_resourcePrefix}.{assetName}.xnb")
           ?? throw new ContentLoadException($"Embedded asset '{_resourcePrefix}.{assetName}.xnb' not found.");
}

public static class EngineAssets
{
    private static readonly Assembly Assembly = typeof(EngineAssets).Assembly;

    public static SpriteFont LoadFont(this SSLGame game, string name)
        => new EmbeddedContentManager(game, Assembly, "SKSSL.Assets").Load<SpriteFont>(name);
}