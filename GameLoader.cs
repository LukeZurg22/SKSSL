using System.Reflection;
using Microsoft.Xna.Framework.Content;

namespace SKSSL;
public static class GameLoader
{
    private static readonly Dictionary<string, string> GAME_PATHS = new();
    public static readonly string GAME_ENVIRONMENT_FOLDER = AppContext.BaseDirectory;
    public static readonly string PROJECT_DIRECTORY = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", ".."));
    public static readonly string DEFAULT_FOLDER_GAME = Path.Combine(GAME_ENVIRONMENT_FOLDER, "game");
    public static readonly string DEFAULT_FOLDER_MODS = Path.Combine(GAME_ENVIRONMENT_FOLDER, "mods");
    
    /// <summary>
    /// Default Localization path for the game.
    /// <example>GameName/game/localization/...</example>
    /// </summary>
    public static string FOLDER_LOCALIZATION = Path.Combine(DEFAULT_FOLDER_GAME, "localization");

    /// <summary>
    /// Returns the default localization folder path, and can also do so for modded localization.
    /// This is a helper method to make getting the path less cumbersome for projects that share
    /// the exact same folder layout.
    /// </summary>
    /// <param name="isMod"></param>
    /// <returns></returns>
    public static string GetDefaultLocalizationFolder(bool isMod = false)
        => !isMod ? FOLDER_LOCALIZATION : Path.Combine(DEFAULT_FOLDER_MODS, "localization");

    /// <summary>
    /// Returns enumerated files from a specific folder path respectful to the program's executable.
    /// Will attempt to use the Application Context's Base Directory as its root if a directory is not provided.
    /// </summary>
    public static IEnumerable<string> GetGameFiles(string? directory = null, params string[] path_s)
    {
        string fullPath = Path.Combine(directory ?? AppContext.BaseDirectory, Path.Combine(path_s));
        return Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
    }
    
    /// <summary>
    /// Retrieves an asset from the content pipeline manually using a provided filepath.
    /// </summary>
    /// <param name="contentManager"></param>
    /// <param name="path"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetPipelineAsset<T>(ContentManager contentManager, params string[] path)
    {
        string assetPath = Path.Combine(path);
        return contentManager.Load<T>(assetPath);
    }
    
    /// <returns>Dedicated path to game files.</returns>
    public static string GPath(params string[] path)
    {
        var dynamicPath = GetPath("FOLDER_GAME");
        return string.IsNullOrEmpty(dynamicPath)
            ? Path.Combine(new[] { DEFAULT_FOLDER_GAME }.Concat(path).ToArray())
            : dynamicPath;
    }

    /// <returns>A game-path explicitly for the "mods" folder.</returns>
    /// <seealso cref="GPath"/>
    public static string MPath(params string[] path)
    {
        var dynamicPath = GetPath("FOLDER_MODS");
        return string.IsNullOrEmpty(dynamicPath)
            ? Path.Combine(new[] { DEFAULT_FOLDER_MODS }.Concat(path).ToArray())
            : dynamicPath;
    }

    public static string Proj(params string[] path)
    {
        var dynamicPath = GetPath("PROJECT_DIRECTORY");
        return string.IsNullOrEmpty(dynamicPath)
            ? Path.Combine(new[] { PROJECT_DIRECTORY }.Concat(path).ToArray())
            : dynamicPath;
    }
    
    private static string? GetPath(string id)
    {
        GAME_PATHS.TryGetValue(id, out var result);
        return result;
    }

    /// <summary>
    /// Initializes the game's two primary directories.
    /// </summary>
    public static void Initialize(params (string id, string path)[] paths)
    {
        // Loop over every tuple and add the provided path to GAME_PATHS. Invalid paths are the programmer's problem.
        foreach ((string id, string path) path in paths) GAME_PATHS[path.id] = path.path;
    }
    
    public delegate void GameLoadAction(string basePath);

    // Store: name (for logging), pathConstant (e.g. FOLDER_RACES), and the loader
    private static readonly List<(string Name, string PathConstant, GameLoadAction Loader)> _loaders = [];
    
    // Public read-only view (optional, for debugging)
    public static IReadOnlyList<(string Name, string PathConstant, GameLoadAction Loader)> Loaders => _loaders.AsReadOnly();

    /// <summary>
    /// Register a loader with its own path constant.
    /// Called from each factory/registry's static constructor.
    /// </summary>
    public static void Register(string name, string pathConstant, GameLoadAction loader)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        if (string.IsNullOrWhiteSpace(pathConstant)) throw new ArgumentException("Path constant required", nameof(pathConstant));
        ArgumentNullException.ThrowIfNull(loader);

        // override if same name registered again (good for mods)
        _loaders.RemoveAll(l => l.Name == name);
        _loaders.Add((name, pathConstant, loader));
    }

    /// <summary>
    /// Load all registered content using the provided pather (base game or mod).
    /// </summary>
    public static void Load(Func<string, string> pather)
    {
        GlobalClean();

        if (_loaders.Count == 0)
            DustLogger.Log("There are no loaders available for the Game Loader!", DustLogger.LOG.GENERAL_WARNING);
        
        foreach ((string name, string pathConstant, GameLoadAction loader) in _loaders)
        {
            try
            {
                string fullPath = pather(pathConstant);
                loader(fullPath);
                DustLogger.Log($"Loaded {name} from: {fullPath}");
            }
            catch (Exception ex)
            {
                DustLogger.Log($"Failed to load {name}: {ex.Message}\n{ex.StackTrace}", 3);
            }
        }

        //IMPL: Implement dynamic localization loading for mods. Overwrite existing localization entries!
            // TODO: Load mod localization as if it were game content. Using Loc.Initialize() again may be handy.
        //LoadModLocalization(pather(FOLDER_LOCALIZATION));
    }
    
    private static void GlobalClean()
    {
        Type loaderType = typeof(IGameFileLoader);

        var staticLoaders = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsClass 
                        && t.IsAbstract 
                        && t.IsSealed   // static class
                        && loaderType.IsAssignableFrom(t))
            .ToArray();

        foreach (Type loader in staticLoaders)
        {
            // Get HandleClean method in all instances of this interface. 
            MethodInfo? cleanMethod = loader.GetMethod("HandleClean", BindingFlags.Static & BindingFlags.Public);
            if (cleanMethod != null)
                cleanMethod.Invoke(null, null); // Run it!
            
            DustLogger.Log($"Triggering Cleanup method for class {loader.Name}");
        }
    }
}