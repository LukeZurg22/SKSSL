using System;
using System.IO;
using System.Linq;
using SKSSL.Extensions;
using SKSSL.Serializing;
using static System.IO.Directory;
using static System.IO.SearchOption;

namespace SKSSL;

/// <summary>
/// Serialized paths to material files.
/// </summary>
/// <seealso cref="TextureMap"/>
public class SerializedMaterial
{
    // ReSharper disable once UnusedMember.Global
    public FileInfo this[TextureMap map]
    {
        get => Files[(byte)map];
        set => Files[(byte)map] = value;
    }

    /// <summary>
    /// Storing as many instances of file data as there are texture types to utilize.
    /// </summary>
    public readonly FileInfo[] Files = new FileInfo[Enum.GetValues<TextureMap>().Length];
}

/// <summary>
/// Generic texture loader for all game asset categories (objects, items, UI, etc.).
/// Supports multi-texture maps (diffuse + normal + etc.) and automatic error texture fallback.
/// </summary>
public class TextureLoader() : IGameLoader(".png", ".jpg")
{
    /// MATERIAL (multiple different mapping images)
    private const string IndicatorMaterial = ".m";

    /// TILEMAP (one sheet of multiple image segments)
    private const string IndicatorTilemap = ".t"; // TODO: Current unused. Add Tilemap support. Would be nice.

    /// ICON (flat image)
    private const string IndicatorIcon = ".i";

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
        // Process all folders inside textures folder. This accommodates nested material folders for whatever reason.
        var folders = GetDirectories(directory, "*", AllDirectories)
            .Where(x =>
                x.EndsWith(IndicatorIcon, StringComparison.OrdinalIgnoreCase) ||
                x.EndsWith(IndicatorMaterial, StringComparison.OrdinalIgnoreCase) ||
                x.EndsWith(IndicatorTilemap, StringComparison.OrdinalIgnoreCase));

        foreach (string folder in folders)
        {
            var folderName = Path.GetRelativePath(directory, folder).ToLowerInvariant();

            string? indicator = null;

            //@formatter:off
            /*  ICONS       */ if (folderName.EndsWith(IndicatorIcon, StringComparison.OrdinalIgnoreCase)) indicator = IndicatorIcon;
            /*  MATERIALS   */ else if (folderName.EndsWith(IndicatorMaterial, StringComparison.OrdinalIgnoreCase)) indicator = IndicatorMaterial;
            /*  TILEMAPS    */ else if (folderName.EndsWith(IndicatorTilemap, StringComparison.OrdinalIgnoreCase)) indicator = IndicatorTilemap;
            //@formatter:on

            if (indicator == null)
            {
                Log($"Texture storage format {folderName} not supported.", LOG.SYSTEM_WARNING);
                continue;
            }

            // Trust that the game constructor created the texture registry. Mostly for debugging and special
            //  utility as an instanced registry, as opposed to static.
            TextureManager textureManager = SSLGame.TextureManager;

            // Get all surface-level files.
            var files = GetFiles(folder, TopDirectoryOnly);
            folderName = folderName[..^indicator.Length];
            switch (indicator)
            {
                case IndicatorIcon:
                    foreach (var file in files) RecordIcon(folderName, file, textureManager);
                    break;
                case IndicatorMaterial:
                    foreach (var file in files) RecordMaterial(folderName, file, textureManager);
                    break;
                // TODO: Add TileMaps.
                default:
                    Log($"Format \'{indicator}\' not supported.", LOG.SYSTEM_WARNING);
                    break;
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

    private static void RecordIcon(string folder, string file, TextureManager textureManager)
    {
        textureManager.RegisterIcon(handle.Create(folder, file), new FileInfo(file));
    }

    private static void RecordMaterial(string folder, string file, TextureManager textureManager)
    {
        string fileNameNoExt = Path.GetFileNameWithoutExtension(file);
        string baseName = fileNameNoExt.RemoveUnderscoreEndingTag();
        string suffix = fileNameNoExt.GetUnderscoreEndingTag();

        // If there is no suffix, assume diffuse. If multiple, well... that sucks! You shouldn't have multiple diffuse
        //  textures in a single material!
        if (!Enum.TryParse(suffix, true, out TextureMap textureType))
            textureType = TextureMap.DIFFUSE;

        // Unique material key (folderPrefix + baseName) -> allows overrides from mods.
        // This material key is the basic handle of a material; no texture typing applied.
        var handle = new handle(Path.Combine(folder, baseName));
        textureManager.RegisterMaterial(handle, textureType, new FileInfo(file));
    }
}