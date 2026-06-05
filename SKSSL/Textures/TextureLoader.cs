using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using static System.IO.Directory;
using static System.IO.SearchOption;
using static SKSSL.Textures.TextureLoader.MaterialRegistry;

namespace SKSSL.Textures;

/// <summary>
/// Supported texture-types in the system. Defaults to <see cref="DIFFUSE"/>.
/// </summary>
/// <remarks>
/// This will not inherently do anything besides permit additional map types. Rendering must be implemented separately.
/// </remarks>
public enum TextureType : byte
{
    /// Plain color information.
    DIFFUSE = 0,

    /// Normal-data.
    NORMAL = 1,

    /// Height data.
    DISPLACEMENT = 2,

    /// Glow data.
    EMISSIVE = 3,

    // Unused as of 20260106
    //GLOSSY,
}

/// <summary>
/// Generic texture loader for all game asset categories (objects, items, UI, etc.).
/// Supports multi-texture maps (diffuse + normal + etc.) and automatic error texture fallback.
/// </summary>
public abstract partial class TextureLoader
{
    private const string IndicatorMaterial = ".m";
    private const string IndicatorTilemap = ".t"; // TODO: Current unused. Tilemap support would be nice.
    private const string IndicatorIcon = ".i";

    /// Generic storage: category -> (texture name -> texture object). These are textures actively being used in memory.
    private static readonly ConcurrentDictionary<string, Dictionary<string, Texture2D>> _textures = new();

    /// <summary>
    /// Initializes texture loaded. An alternative version of the loaded with a custom implement for
    /// <br/><br/>
    /// It is IMPERATIVE that this be loaded before the base.Initialize() of the game's Initialize() method.
    /// </summary>
    /// <param name="directory">A particular game directory's textures folder.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Load(string directory)
    {
        // Process all folders inside textures folder. 
        var subFolders = GetDirectories(directory, "", AllDirectories);
        foreach (string texturesFolder in subFolders)
        {
            /*
             * I have > 1 options when handling this. All textures could be explicitly unique by relative filepath to
             * the root of their local game directory. (I.e. textures --> test/<name>.<ext>/{files} <-
             *  However, this means all mods / loaded directories that intend to override the base directory must too
             *  mimic the exact texture path respectful to their own root.
             * The alternative is only reading <name>.<ext> as the indexer, which marks total exclusivity for that
             * folder name, but allows mods to easily override other game content regardless of the way they organize
             * the layout of their textures!
             */
            
            // Ensure that this folder is a valid one.
            var folderName = Path.GetFileName(texturesFolder).ToLowerInvariant();
            if (folderName.EndsWith(IndicatorMaterial))
            {
                folderName = folderName[..^IndicatorMaterial.Length];
                var files = EnumerateFiles(texturesFolder, "*", TopDirectoryOnly);

                // MATERIALS!
                foreach (var file in files) LoadTextureMaterial(file, folderName);
            }
            else if (folderName.EndsWith(IndicatorIcon))
            {
                folderName = folderName[..^IndicatorMaterial.Length];
                var files = EnumerateFiles(texturesFolder, "*", TopDirectoryOnly);

                // ICONS!
                foreach (var file in files) LoadTextureIcon(file, folderName);
            }
        }

        // Due to how it is written, all texture categories must be registered.
        // I.e. "items" must have a dedicated "items" folder.
        // Builds Monogame Content.
        BuildContentIndex();
    }

    /// <summary>
    /// Core loading logic that attempts to load from a file path, then Content pipeline, before returning an
    /// error texture of nothing was successful. This is generic, working between all kinds of map types.
    /// </summary>
    private static Texture2D LoadFromFileOrContent(string filePath, string cacheKey, string category)
    {
        Texture2D texture;
        // 1. Direct file load (for mod/override support)
        if (File.Exists(filePath))
        {
            try
            {
                if (_textures.TryGetValue(category, out var categoryDictionary))
                {
                    if (categoryDictionary.TryGetValue(cacheKey, out Texture2D? cached))
                        return cached;
                }
                
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 4096,
                    FileOptions.SequentialScan);

                texture = Texture2D.FromStream(SSLGame.Graphics, stream).ToMipMapped();
                return texture;
            }
            catch (Exception ex)
            {
                Log($"Failed direct load: {filePath} - {ex.Message}", LOG.FILE_WARNING);
            }
        }

        // 2. Fallback to MonoGame Content pipeline (.xnb)
        foreach (ContentManager contentManager in SSLGame.Instance.ContentManagers)
        {
            try
            {
                texture = contentManager.Load<Texture2D>(cacheKey).ToMipMapped();
                return texture;
            }
            catch
            {
                //
            }
        }

        // 3. Error texture fallback
        Log($"Texture load failed: {cacheKey} (category: {category}) → error texture", LOG.FILE_WARNING);
        return HardcodedTextures.GetErrorTexture();
    }

    /// <summary>
    /// Loads a provided asset name as a <see cref="Texture2D"/>.
    /// Assumes the folders within the TextureLoader are all texture folders.
    /// </summary>
    /// <param name="key">Name of the provided asset without extension. (e.g. "Textures/PlayerSprite")</param>
    /// <param name="category">Dedicated category.</param>
    /// <param name="path">Path to asset. Nullable for external calls.</param>
    /// <returns>Texture asset or Default Error Texture, instead.</returns>
    public static Texture2D LoadAsset(string key, string? category = null, string? path = null)
    {
        // WARN: Loading an item / non-material texture from content repository may be a bit wonky.

        // Fast path: cached lookup
        if (category != null && _textures.TryGetValue(category, out var dict) &&
            dict.TryGetValue(key, out Texture2D? cached))
            return cached;

        // Brute force if no category provided
        if (category == null)
        {
            foreach (var catDict in _textures.Values)
                if (catDict.TryGetValue(key, out Texture2D? tex))
                    return tex;
        }

        // Explicit path provided (useful for one-off loads)
        if (path != null && File.Exists(path))
            return LoadFromFileOrContent(path, key, category ?? "unknown");

        Log($"Texture not found: [{category}:{key}]", LOG.FILE_WARNING);
        return HardcodedTextures.GetErrorTexture();
    }

    /// <summary>
    /// Get read-only dictionary for a category.
    /// </summary>
    public static IReadOnlyDictionary<string, TTexture> GetCategory<TTexture>(string categoryName)
    {
        if (_textures.TryGetValue(categoryName, out var dict))
            return dict.AsReadOnly() as IReadOnlyDictionary<string, TTexture>
                   ?? new Dictionary<string, TTexture>().AsReadOnly();

        return new Dictionary<string, TTexture>().AsReadOnly();
    }
}