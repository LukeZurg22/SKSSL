using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Extensions;
using static SKSSL.GameManager;
using static SKSSL.Textures.TextureLoader.MaterialRegistry;

// ReSharper disable SwitchStatementHandlesSomeKnownEnumValuesWithDefault
// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable ClassNeverInstantiated.Global

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
/// <br/><br/>
/// <see cref="InitializeRegistries"/> MUST be filled-out per-implementation based on the
/// developer requirements / layout of the project.
/// Allows the developer to pre-initialize a custom loader for the game, assuming it is on the surface-level of
/// game initialization and before base.Initialize() is called in the game's Initialize() method.
/// </summary>
public abstract partial class TextureLoader
{
    #region Fields & Constructors

    /// Initially default implementation. Permits one static instance per program.
    private static TextureLoader _instance = null!;

    /// Allow override (e.g., for mods or tests)
    public static TextureLoader Instance
    {
        get => _instance;
        set => _instance = value ?? throw new ArgumentNullException(nameof(value));
    }

    private static GraphicsDevice _graphicsDevice { get; set; } = null!;

    /// Generic storage: category -> (texture name -> texture object). These are textures actively being used in memory.
    private static readonly ConcurrentDictionary<string, Dictionary<string, Texture2D>> _textures = new();

    /// Material registry is assumed to be in MaterialRegistry static class
    private static readonly Dictionary<string, TextureCategoryConfig> _categories = new();

    private static bool IsInitialized { get; set; } = false;

    /// Default static assignation of instance of a texture loader.
    public TextureLoader(GraphicsDevice graphicsDevice)
    {
        _instance = this;
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes texture loaded. An alternative version of the loaded with a custom implement for
    /// <br/><br/>
    /// It is IMPERATIVE that this be loaded before the base.Initialize() of the game's Initialize() method.
    /// </summary>
    /// <param name="directory">A particular game directory.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Initialize(GameDirectory directory)
    {
        // If the texture loader has already been initialized by a "surface-level" class override,
        //  then that override is the one that shall be used and whatever is needed has already been initialized.
        if (IsInitialized) return;
        IsInitialized = true;
        _instance.InitializeRegistries();

        // Complete Texture Initialization.
        // Below handles the Initialization (/preloading) of all game data.
        // Also includes mods. This is a trick that will come in handy later~
        string texturesFolder = directory.TexturesFolder;
        // Load all game textures into memory.

        // Load all folders with registered textures.
        // Get all categorical texture folders.
        var subFolders = Directory.GetDirectories(texturesFolder, "", SearchOption.TopDirectoryOnly);
        ProcessTextureSubfolder(subFolders);

        // Due to how it is written, all texture categories must be registered.
        // I.e. "items" must have a dedicated "items" folder.
        // Builds Monogame Content.
        BuildContentIndex();
    }

    /// Process all folders inside textures folder. 
    private static void ProcessTextureSubfolder(IEnumerable<string> subFolders)
    {
        // Extract every texture folder contained in subfolders, and handle differently depending on categories.
        foreach (var subFolder in subFolders)
        {
            // Ensure that this folder is a valid one.
            var folderName = Path.GetFileName(subFolder).ToLowerInvariant();

            // Obtain the first config file whose asset key matches this subfolder.
            TextureCategoryConfig? config =
                _categories.Values.FirstOrDefault(d => subFolder.Contains(d.AssetPathKey));
            if (config is null) continue;

            // Use registered asset paths to find dedicated folder, and load it.
            if (!folderName.Contains(config.AssetPathKey))
                // TODO: Add handling for "rogue" texture folders, who aren't registered.
                continue;

            // Database for specific category, such as "Items" or "Entities", etc.
            if (config.IsMultiTextureMap)
                LoadMaterialTextureCategory(subFolder, config);
            else
                LoadSingleTextureCategory(subFolder, config);
        }
    }

    #endregion

    #region Single Textures

    private static void LoadSingleTextureCategory(string directory, TextureCategoryConfig config)
    {
        if (!_textures.ContainsKey(config.AssetPathKey))
            _textures[config.AssetPathKey] = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

        var files 
            = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories);

        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string baseKey = config.KeyTransform?.Invoke(fileName, file)
                             ?? fileName.ToLowerInvariant();

            // Use category + key to avoid cross-category collisions, but still allow overrides within category
            string fullKey = $"{config.AssetPathKey}/{baseKey}";

            Texture2D texture = LoadFromFileOrContent(file, fullKey, config.AssetPathKey, false);

            // This assignment automatically overrides any previous texture with the same key
            _textures[config.AssetPathKey][fullKey] = texture;
        }
    }

    #endregion

    #region Material Loading (with overrides)

    private static void LoadMaterialTextureCategory(string directory, TextureCategoryConfig config)
    {
        var materialGroups = new Dictionary<string, SKMaterial>(StringComparer.OrdinalIgnoreCase);

        foreach (var texturesFolder in Directory.GetDirectories(directory))
        {
            string folderPrefix = Path.GetFileName(texturesFolder).ToLowerInvariant();
            var files
                = Directory.EnumerateFiles(texturesFolder, "*", SearchOption.AllDirectories);


            foreach (var file in files)
            {
                string fileNameNoExt = Path.GetFileNameWithoutExtension(file);
                string baseName = fileNameNoExt.RemoveUnderscoreEndingTag();
                string suffix = fileNameNoExt.GetUnderscoreEndingTag();

                if (!Enum.TryParse<TextureType>(suffix, true, out var textureType))
                    textureType = TextureType.DIFFUSE;

                // Unique material key (folderPrefix + baseName) -> allows overrides from mods
                string materialKey = config.KeyTransform?.Invoke(folderPrefix, baseName)
                                     ?? $"{folderPrefix}_{baseName}";

                if (!materialGroups.TryGetValue(materialKey, out var material))
                {
                    material = new SKMaterial();
                    materialGroups[materialKey] = material;
                }

                Texture2D texture = LoadFromFileOrContent(file, materialKey, config.AssetPathKey, true);

                // Assign map (later files override earlier ones for the same material + type)
                switch (textureType)
                {
                    case TextureType.DIFFUSE: material.Diffuse = texture; break;
                    case TextureType.NORMAL: material.Normal = texture; break;
                    case TextureType.DISPLACEMENT: material.Displacement = texture; break;
                    case TextureType.EMISSIVE: material.Emissive = texture; break;
                }
            }
        }

        // Register / override materials in the MaterialRegistry
        foreach (var pair in materialGroups)
        {
            RegisterMaterial(pair.Key, pair.Value); // This should internally support override
        }
    }

    #endregion

    /// <summary>
    /// Core loading logic: tries direct file first, then Content pipeline, then error texture.
    /// </summary>
    private static Texture2D LoadFromFileOrContent(string filePath, string cacheKey, string category, bool isMulti)
    {
        Texture2D texture;
        // 1. Direct file load (for mod/override support)
        if (File.Exists(filePath))
        {
            try
            {
                using var stream = File.OpenRead(filePath);
                texture = Texture2D.FromStream(_graphicsDevice, stream).ToMipMapped();

                if (isMulti || category.IsNullOrEmpty())
                    return texture;

                if (!_textures.ContainsKey(category))
                    _textures[category] = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

                _textures[category][cacheKey] = texture; // override happens here

                return texture;
            }
            catch (Exception ex)
            {
                Log($"Failed direct load: {filePath} - {ex.Message}", LOG.FILE_WARNING);
            }
        }

        // 2. Fallback to MonoGame Content pipeline (.xnb)
        foreach (ContentManager contentManager in Game.ContentManagers)
        {
            try
            {
                texture = contentManager.Load<Texture2D>(cacheKey).ToMipMapped();
                if (!isMulti && category.IsNullOrEmpty())
                    _textures[category][cacheKey] = texture;

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

    #region Get Raw Images

    /// <summary>
    /// Loads a provided asset name as a <see cref="Texture2D"/>.
    /// Assumes the folders within the TextureLoader are all texture folders.
    /// </summary>
    /// <param name="key">Name of the provided asset without extension. (e.g. "Textures/PlayerSprite")</param>
    /// <param name="category">Dedicated category.</param>
    /// <param name="isMulti">Is the texture expected to be a material? Toggles local texture caching.</param>
    /// <param name="path">Path to asset. Nullable for external calls.</param>
    /// <returns>Texture asset or Default Error Texture, instead.</returns>
    public static Texture2D Load(string key, string? category = null, bool isMulti = false, string? path = null)
    {
        // WARN: Loading an item / non-material texture from content repository may be a bit wonky.

        // Fast path: cached lookup
        if (category != null && _textures.TryGetValue(category, out var dict) &&
            dict.TryGetValue(key, out var cached))
            return cached;

        // Brute force if no category provided
        if (category == null)
        {
            foreach (var catDict in _textures.Values)
                if (catDict.TryGetValue(key, out var tex))
                    return tex;
        }

        // Explicit path provided (useful for one-off loads)
        if (path != null && File.Exists(path))
            return LoadFromFileOrContent(path, key, category ?? "unknown", isMulti);

        Log($"Texture not found: [{category}:{key}]", LOG.FILE_WARNING);
        return HardcodedTextures.GetErrorTexture();
    }

    /// <summary>
    /// Register a new texture category (e.g., objects, items).
    /// </summary>
    public static void RegisterCategory(TextureCategoryConfig config)
    {
        _categories[config.AssetPathKey] = config;

        // Material mapping is now handled in the Material Registry.
        if (!config.IsMultiTextureMap)
            _textures[config.AssetPathKey] = new Dictionary<string, Texture2D>();
    }

    #endregion

    /// <summary>
    /// Custom method for initializing dedicated registries. Overload required.
    /// </summary>
    /// <remarks>Registries are the dedicated names to the topmost folders containing textures.</remarks>
    protected abstract void InitializeRegistries();

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

    /// <summary>
    /// Slow calls to get material from Material Registry. Not recommended for common or repetitive use. 
    /// </summary>
    public static SKMaterial GetMaterialWithKey(string key) => GetMaterial(GetId(key));
}

/// <summary>
/// Configurable handling for texture registration behaviour.
/// </summary>
public class TextureCategoryConfig
{
    /// Asset path that is checked-over for loading. Also used for categorization.
    /// <remarks>Make sure that this is assigned as lowercase, or whatever case needed to match folder structure</remarks>
    /// <example>e.g., "I.e. "objects", "items" , etc."</example>
    public required string AssetPathKey { get; init; }

    /// Does this texture category store complex texture maps?
    /// <value>Stores simple key-value pairs when false, and a <see cref="SKMaterial"/> dictionary when true.</value>
    /// <remarks>Example layout:<br/>
    /// game ➡<br/>
    /// .textures ➡<br/>
    /// ..test ➡<br/>
    /// ...test.png, test_normal.png, etc.</remarks>
    public bool IsMultiTextureMap { get; init; } = false;

    /// In-line function call to transform string tuple (key, value), returning and assigning a resulting string value. 
    public Func<string, string, string>? KeyTransform { get; init; }
}