using System;
using System.IO;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedType.Global

namespace SKSSL.Assets;

/// <summary>
/// Embedded custom content manager mostly for personal use in SKSSL. When provided with a Service Provider, an
/// assembly to inspect and a dedicated internal embedded resources assets namespace name, it can be used to load
/// internal embedded resources like MonoGame's <see cref="ContentManager"/>.
/// </summary>
public class EmbeddedContentManager : ContentManager
{
    private readonly Assembly _assembly;
    private readonly string _internalEmbeddedAssetsNamespace;

    public EmbeddedContentManager(IServiceProvider services, Assembly assembly, string internalEmbeddedAssetsNamespace) :
        base(services)
    {
        _assembly = assembly;
        _internalEmbeddedAssetsNamespace = internalEmbeddedAssetsNamespace;
    }

    protected override Stream OpenStream(string assetName)
    {
        string resourceName = $"{_internalEmbeddedAssetsNamespace}.{assetName}.xnb";
        Stream? stream = _assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new ContentLoadException(
                $"Embedded asset '{resourceName}' not found.");
        }

        return stream;
    }
}

public static class EngineAssets
{
    private static readonly Assembly Assembly = typeof(EngineAssets).Assembly;

    public static SpriteFont LoadFont(this SSLGame game, string name)
    {
        var content = new EmbeddedContentManager(game.Content.ServiceProvider, Assembly, "SKSSL.Assets");
        var font = content.Load<SpriteFont>(name);
        return font;
    }
}