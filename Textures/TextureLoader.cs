using System.Collections.Concurrent;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities;
// ReSharper disable UnusedMethodReturnValue.Global
// ReSharper disable MemberCanBeProtected.Global

// ReSharper disable ClassNeverInstantiated.Global

namespace SKSSL.Textures;

// Default implementation
public class DefaultTextureLoader : TextureLoader
{
    protected override Texture2D GetTextureImplement<T>(string fullFilePath)
    {
        TryGetTextureFromFile(fullFilePath, out Texture2D texture);
        return texture;
    }

    protected override void CustomInitializeRegistries() =>
        throw new NotImplementedException(
            "Developer(s) MUST implement custom Registries Initialization, as registries may vary between projects.");

    protected override void CustomOptionalLoad(string input)
    {
    }
}

/// <summary>
/// Generic texture loader for all game asset categories (blocks, items, UI, etc.).
/// Supports multi-texture maps (diffuse + normal + etc.) and automatic error texture fallback.
/// <br/><br/>
/// <see cref="CustomInitializeRegistries"/> MUST be filled-out per-implementation based on the
/// developer requirements / layout of the project.
/// </summary>
public abstract class TextureLoader
{
    // Default implementation
    private static TextureLoader _instance = new DefaultTextureLoader();

    // Allow override (e.g., for mods or tests)
    public static TextureLoader Instance
    {
        get => _instance;
        set => _instance = value ?? throw new ArgumentNullException(nameof(value));
    }

    public static GraphicsDevice _graphicsDevice { get; private set; } = null!;

    private static bool IsInitialized { get; set; } = false;

    /// <summary>
    /// Initializes texture loaded. An alternative version of the loaded with a custom implement for
    /// <see cref="GetTextureImplement{T}"/> may be provided to override the <see cref="DefaultTextureLoader"/>.
    /// <br/><br/>
    /// It is IMPERATIVE that this be loaded before the base.Initialize() of the game's Initialize() method.
    /// </summary>
    /// <param name="graphicsDevice"></param>
    /// <param name="alternativeLoader"></param>
    /// <exception cref="ArgumentNullException"></exception>
    public static void Initialize(GraphicsDevice graphicsDevice, TextureLoader? alternativeLoader = null)
    {
        // If the texture loader has already been initialized by a "surface-level" class override,
        //  then that override is the one that shall be used and whatever is needed has already been initialized.
        if (IsInitialized)
            return;

        if (alternativeLoader != null)
            _instance = alternativeLoader;

        // Load Custom Registries.
        _instance.CustomInitializeRegistries();

        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        IsInitialized = true;
    }

    // The "static" method — but delegates to instance
    public static Texture2D GetTexture<T>(string fullFilePath, bool isModded = false) where T : class
        => Instance.GetTextureImplement<T>(fullFilePath);

    #region Get Raw Images

    /// <summary>
    /// Loads a Texture2D directly from a file path (PNG, JPG, BMP, etc.).
    /// Returns boolean and outputs <see cref="HardcodedAssets"/> error texture on failure.
    /// </summary>
    public static bool TryGetTextureFromFile(string filePath, out Texture2D texture)
    {
        if (!File.Exists(filePath))
        {
            DustLogger.Log($"Texture file not found: {filePath}", 3);
            
            texture = HardcodedAssets.GetErrorTexture();
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(filePath);
            texture = Texture2D.FromStream(_graphicsDevice, stream);

            // Optional: Set sensible defaults
            texture.Name = Path.GetFileNameWithoutExtension(filePath);

            return true;
        }
        catch (Exception ex)
        {
            DustLogger.Log($"Failed to load texture from {filePath}: {ex.Message}", 3);
            texture = HardcodedAssets.GetErrorTexture();
            return false;
        }
    }

    /// <summary>
    /// Async version (for large files or many loads)
    /// </summary>
    public static async Task<Texture2D> LoadFromFileAsync(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Texture file not found: {filePath}");
            return HardcodedAssets.GetErrorTexture();
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            // FromStream is synchronous — wrap in Task.Run for async I/O
            return await Task.Run(() => Texture2D.FromStream(_graphicsDevice, stream));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load texture from {filePath}: {ex.Message}");
            return HardcodedAssets.GetErrorTexture();
        }
    }

    #endregion

    /// <summary>
    /// Overridable Texture acquisition.
    /// </summary>
    protected abstract Texture2D GetTextureImplement<T>(string fullFilePath) where T : class;

    /// <summary>
    /// Custom method for initializing dedicated registries. Absolutely required per-project.
    /// </summary>
    protected abstract void CustomInitializeRegistries();

    /// <summary>
    /// Custom method for loading. This is additional optional logic that the developer may choose to implement.
    /// Though all instantiated inheritors of <see cref="TextureLoader"/> require this, the developer is NOT
    /// required to add any code.
    /// </summary>
    protected abstract void CustomOptionalLoad(string input);

    // Generic storage: category → texture name → texture object
    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>>
        _textureDatabases = new();

    private static readonly Dictionary<string, TextureCategoryConfig> _categories = new();

    /// <summary>
    /// Register a new texture category (e.g., blocks, items).
    /// </summary>
    public static void RegisterCategory<TTexture>
        (string categoryName, TextureCategoryConfig config) where TTexture : class
    {
        _categories[categoryName] = config;
        _textureDatabases.GetOrAdd(categoryName, _ => new ConcurrentDictionary<string, object>());
    }

    /// <summary>
    /// Get read-only dictionary for a category.
    /// </summary>
    public static IReadOnlyDictionary<string, TTexture> GetCategory<TTexture>(string categoryName)
    {
        if (_textureDatabases.TryGetValue(categoryName, out var dict))
        {
            return (IReadOnlyDictionary<string, TTexture>)dict.AsReadOnly();
        }

        return new Dictionary<string, TTexture>().AsReadOnly();
    }

    /// <summary>
    /// Ambiguous current directory, which may be a game or mod directory.
    /// </summary>
    private static string _currentDirectory = string.Empty;

    /// <summary>
    /// Load all registered texture categories.
    /// </summary>
    public static void LoadAll(string currentDirectory)
    {
        _currentDirectory = currentDirectory;
        _instance.CustomOptionalLoad(currentDirectory);
        foreach ((string categoryName, TextureCategoryConfig config) in _categories)
            LoadCategory(categoryName, config);
    }

    private static void LoadCategory(string categoryName, TextureCategoryConfig config)
    {
        var database = _textureDatabases[categoryName];

        if (config.IsMultiTextureMap)
            LoadMultiTextureCategory(categoryName, config, database);
        else
            LoadSingleTextureCategory(categoryName, config, database);
    }

    private static void LoadSingleTextureCategory(
        string categoryName,
        TextureCategoryConfig config,
        ConcurrentDictionary<string, object> database)
    {
        string dir = _currentDirectory;
        if (config.AssetPathKey != null)
            dir = Path.Combine(_currentDirectory, config.AssetPathKey);

        var files = GameLoader.GetGameFiles(dir);

        foreach (var file in files)
        {
            string fileName = Path.GetFileNameWithoutExtension(file);
            string key = config.KeyTransform?.Invoke(fileName, file) ?? fileName.ToLower();

            Texture2D texture = _instance.GetTextureImplement<Texture2D>(file);
            database[key] = texture; // Error Reporting & Texture is automatically handled in the call.
        }
    }


    private static void LoadMultiTextureCategory(string categoryName, TextureCategoryConfig config,
        ConcurrentDictionary<string, object> database)
    {
        string dir = _currentDirectory;
        if (config.AssetPathKey != null)
            dir = Path.Combine(_currentDirectory, config.AssetPathKey);

        string[] directories = Directory.GetDirectories(dir);

        foreach (var folder in directories)
        {
            string blockName = Path.GetFileName(folder).ToLower();
            var files = GameLoader.GetGameFiles(null, [folder]);

            TextureMaps currentMap = new();
            string currentKey = string.Empty;

            foreach (var file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                Texture2D texture = _instance.GetTextureImplement<Texture2D>(file);

                TextureMaps.TextureType subType =
                    config.SubTextureClassifier?.Invoke(fileName) ?? TextureMaps.TextureType.DIFFUSE;

                switch (subType)
                {
                    case TextureMaps.TextureType.DIFFUSE:
                        // Finalize previous map
                        if (!string.IsNullOrEmpty(currentKey))
                        {
                            database[currentKey] = FinalizeMap(currentMap, categoryName);
                        }

                        // Start new map
                        currentMap = new TextureMaps { Diffuse = texture };
                        currentKey = config.KeyTransform?.Invoke(blockName, file) ?? $"{blockName}_{fileName}";
                        break;

                    case TextureMaps.TextureType.NORMAL:
                        currentMap.Normal = texture;
                        break;

                    // TODO: Add later.
                    // case TextureMaps.TextureType.DISPLACEMENT:
                    //     currentMap.Displacement = texture;
                    //     break;
                    // case TextureMaps.TextureType.GLOSSY:
                    //     currentMap.Glossy = texture;
                    //     break;

                    default:
                        DustLogger.Log($"Unknown sub-texture type for {fileName}", 3);
                        break;
                }
            }

            // Finalize last map
            if (!string.IsNullOrEmpty(currentKey))
            {
                database[currentKey] = FinalizeMap(currentMap, categoryName);
            }
        }
    }

    private static TextureMaps FinalizeMap(TextureMaps map, string categoryName)
    {
        // Ensure all required textures exist (fallback to error)
        map.Diffuse ??= HardcodedAssets.GetErrorTexture();
        map.Normal ??= HardcodedAssets.GetErrorTexture();
        //map.Displacement ??= HardcodedTextures.GetErrorTexture2D();
        //map.Glossy ??= HardcodedTextures.GetErrorTexture2D();

        return map;
    }

    /// <summary>
    /// Safe accessor with error fallback and logging.
    /// </summary>
    public static T GetTexture<T>(string categoryName, string key) where T : class
    {
        if (_textureDatabases.TryGetValue(categoryName, out var dict) &&
            dict.TryGetValue(key, out var texture) &&
            texture is T result)
        {
            return result;
        }

        if (!key.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"Missing texture: [{categoryName}] \"{key}\" — using error texture.");
        }

        return (T)(object)HardcodedAssets.GetErrorTexture();
    }
}

public class TextureCategoryConfig
{
    public string? AssetPathKey { get; init; } // e.g., "__ASSETS_TEXTURES_ITEMS"
    public bool IsMultiTextureMap { get; init; }
    public Func<string, string, string>? KeyTransform { get; init; }
    public Func<string, TextureMaps.TextureType>? SubTextureClassifier { get; init; }
}