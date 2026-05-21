using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Xna.Framework.Content;
using static SKSSL.DustLogger;

namespace SKSSL;

/// <summary>
/// File path handling.
/// </summary>
public static class StaticGameLoader
{
    private static readonly Dictionary<string, string> GAME_PATHS = new();

    /// Root directory of the game's folder that includes the executable.
    public static readonly string GAME_ENVIRONMENT_FOLDER = AppContext.BaseDirectory;

    /// Root directory of the Game in its development. NOT the final published application!
    public static readonly string PROJECT_DIRECTORY =
        Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", ".."));

    /// GameFolder/game/...
    public static readonly string DEFAULT_FOLDER_GAME = Path.Combine(GAME_ENVIRONMENT_FOLDER, "game");

    /// GameFolder/mods/...
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
    /// <remarks>
    /// Directory is optional, as it will use the game's base application directory instead if
    /// not provided.
    /// </remarks>
    public static IEnumerable<string> GetGameFiles(string? directory = null, params string[] path_s)
    {
        string fullPath = Path.Combine(directory ?? AppContext.BaseDirectory, Path.Combine(path_s));
        return Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories);
    }

    /// <summary>
    /// Retrieves an asset from the content pipeline manually using a provided filepath.
    /// </summary>
    /// <param name="contentManager"></param>
    /// <param name="path_s"></param>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public static T GetPipelineAsset<T>(ContentManager contentManager, params string[] path_s)
    {
        string assetPath = Path.Combine(path_s);
        return contentManager.Load<T>(assetPath);
    }

    /// <returns>Dedicated path to game files.</returns>
    public static string GPath(params string[] path)
    {
        var dynamicPath = GetStaticPath("FOLDER_GAME");
        return string.IsNullOrEmpty(dynamicPath)
            ? Path.Combine(new[] { DEFAULT_FOLDER_GAME }.Concat(path).ToArray())
            : dynamicPath;
    }

    /// <returns>A game-path explicitly for the "mods" folder.</returns>
    /// <seealso cref="GPath"/>
    public static string MPath(params string[] path)
    {
        var dynamicPath = GetStaticPath("FOLDER_MODS");
        return string.IsNullOrEmpty(dynamicPath)
            ? Path.Combine(new[] { DEFAULT_FOLDER_MODS }.Concat(path).ToArray())
            : dynamicPath;
    }

    private static string? GetStaticPath(string id)
    {
        GAME_PATHS.TryGetValue(id, out var result);
        return result;
    }

    /// <summary>
    /// Retrieves all mod directories in the game.
    /// </summary>
    /// <returns></returns>
    public static string[] GetAllModDirectories() => Directory.GetDirectories(MPath());

    /// <summary>
    /// Retrieves all directories in the game, including modded.
    /// </summary>
    /// <returns></returns>
    public static IEnumerable<GameContentDirectory> GetAllGameDirectories()
    {
        var game = GPath();
        var mods = Directory.GetDirectories(MPath());
        var all = mods.Prepend(game);

        List<GameContentDirectory> result = [];
        foreach (var directory in all)
        {
            var gcd = new GameContentDirectory(directory);
            result.Add(gcd);
        }

        return result;
    }

    /// Initializes the game's static paths.
    public static void Initialize(params (string id, string path)[] paths)
    {
        // Loop over every tuple and add the provided path to GAME_PATHS. Invalid paths are the programmer's problem.
        foreach ((string id, string path) path in paths)
        {
            GAME_PATHS[path.id] = path.path;
        }
    }

    /// Called on game load.
    public delegate void GameLoadAction(string basePath);

    /// Store: name (for logging), pathConstant (e.g. FOLDER_RACES), and the loader
    private static readonly List<(string Name, string PathConstant, GameLoadAction Loader)> _loaders = [];

    /// Public read-only view (optional, for debugging)
    public static IReadOnlyList<(string Name, string PathConstant, GameLoadAction Loader)> Loaders =>
        _loaders.AsReadOnly();

    /// <summary>
    /// Register a loader with its own path constant.
    /// Called from each factory/registry's static constructor.
    /// </summary>
    public static void Register(string name, string pathConstant, GameLoadAction loader)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required", nameof(name));
        if (string.IsNullOrWhiteSpace(pathConstant))
            throw new ArgumentException("Path constant required", nameof(pathConstant));
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
            Log("There are no loaders available for the Game Loader!", LOG.GENERAL_WARNING);

        foreach ((string name, string pathConstant, GameLoadAction loader) in _loaders)
        {
            try
            {
                string fullPath = pather(pathConstant);
                loader(fullPath);
                // TODO: Replace this w. game content system. The pather is worthless now.
                Log($"Loaded {name} from: {Path.GetFileName(fullPath)}");
            }
            catch (Exception ex)
            {
                Log($"Failed to load {name}: {ex.Message}\n{ex.StackTrace}", 3);
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
                        && t.IsSealed // static class
                        && loaderType.IsAssignableFrom(t))
            .ToArray();

        foreach (Type loader in staticLoaders)
        {
            // Get HandleClean method in all instances of this interface. 
            MethodInfo? cleanMethod = loader.GetMethod("HandleClean", BindingFlags.Static & BindingFlags.Public);
            if (cleanMethod != null)
                cleanMethod.Invoke(null, null); // Run it!

            Log($"Triggering Cleanup method for class {loader.Name}");
        }
    }
}