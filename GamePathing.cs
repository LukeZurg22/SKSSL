#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
namespace SKSSL;

public static class GamePathing
{
    public static readonly string GAME_ENVIRONMENT = Path.Combine(Environment.CurrentDirectory);
    
    private static string FOLDER_GAME;
    private static string FOLDER_MODS;
    
    /// <returns>Dedicated path to game files.</returns>
    public static string GPath(params string[] path) => Path.Combine(new[] { FOLDER_GAME }.Concat(path).ToArray());

    /// <returns>A game-path explicitly for the "mods" folder.</returns>
    /// <seealso cref="GPath"/>
    public static string MPath(params string[] path) => Path.Combine(new[] { FOLDER_MODS }.Concat(path).ToArray());
    
    /// <summary>
    /// Initializes the game's 
    /// </summary>
    /// <param name="gameDirectory"></param>
    /// <param name="modsDirectory"></param>
    public static void Initialize(string gameDirectory, string modsDirectory)
    {
        FOLDER_GAME = Path.Combine(GAME_ENVIRONMENT, gameDirectory);
        FOLDER_MODS = Path.Combine(GAME_ENVIRONMENT, modsDirectory);
    }
}