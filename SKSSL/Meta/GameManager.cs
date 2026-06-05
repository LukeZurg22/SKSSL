namespace SKSSL;

// ReSharper disable UnusedMember.Global
/// <summary>
/// Static class game manager used to run and handle total game instance.
/// </summary>
public static class GameManager
{
    /// Topmost game instance reverse-accessible from lower ends of the call-chain.

    /// Title of game.
    public static string GameName = "SKSSL";

    /// Force game closure.
    public static void Exit()
    {
        // Safely exit without suicidal tendencies.
        SSLGame.Quit();
    }

    /// Force game status reset.
    public static void ResetGame() => SSLGame.ResetGame();

    /// Run the game instance.
    public static void Run<T>(string gameName) where T : SSLGame, new()
    {
        GameName = gameName;
        
        // Safely run.
        using var Game = new T();
        Game.Run();
    }
}