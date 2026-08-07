using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using static System.IO.Directory;
using static System.IO.SearchOption;

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
public partial class TextureLoader() : IGameLoader([".png", ".jpg"])
{
    private const string IndicatorMaterial = ".m";
    private const string IndicatorTilemap = ".t"; // TODO: Current unused. Tilemap support would be nice.
    private const string IndicatorIcon = ".i";

    /// Generic storage: category -> (texture name -> texture object). These are textures actively being used in memory.
    private static readonly ConcurrentDictionary<string, Dictionary<string, Texture2D>> _textures = new();

    /// Stores whether content from XNA / Monogame Content folders have been built.
    private static bool _contentIndexBuilt = false;

    /// <summary>
    /// Initializes texture loaded. An alternative version of the loaded with a custom implement for
    /// <br/><br/>
    /// It is IMPERATIVE that this be loaded before the base.Initialize() of the game's Initialize() method.
    /// </summary>
    /// <param name="directory">A particular game directory's textures folder.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public override void Load(string directory)
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
             *
             * I subsequently decided to go with the first option. Declarative overrides seemed appealing to me as of
             * 202606050522, so I am going with that. I could have it toggle-able, and it would be very easy to do, but
             * for now it isn't necessary and it could come up in the future.
             *
             * If it DOES come up in the future, simply add an IF-statement to the assignment of the folder name, where
             * it will choose between GetRelativePath() or GetFileName(); both calling ToLowerInvariant().
             */

            // Ensure that this folder is a valid one.
            var folderName = Path.GetRelativePath(directory, texturesFolder).ToLowerInvariant();
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
        // Build XNA / Monogame content.
        // WIP: Ensure this actually works, and simplify external content loaders. Possibly with Source Generators?
        if (_contentIndexBuilt) return;
        _contentIndexBuilt = true;

        XNAContentLoader.BuildContentIndex();
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
                if (_textures.TryGetValue(category, out var categoryDictionary) &&
                    categoryDictionary.TryGetValue(cacheKey, out Texture2D? cached)) return cached;

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
}