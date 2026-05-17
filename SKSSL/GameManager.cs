namespace SKSSL;

/// <summary>
/// Static class game manager used to run and handle total game instance.
/// </summary>
public static class GameManager
{
    /// Topmost game instance reverse-accessible from lower ends of the call-chain.
    public static SSLGame Game { get; private set; } = null!;
    
    /// Title of game window.
    public static string Title => Game.Title;
    
    /// Aspect ratio to render the game.
    public static float AspectRatio => Game.GraphicsDevice.Viewport.AspectRatio;

    /// Force game closure.
    public static void Exit()
    {
        // Safely exit without suicidal tendencies.
        SSLGame game = Game;
        SSLGame.Quit();
        game.Exit();
    }
    
    /// Force game status reset.
    public static void ResetGame() => SSLGame.ResetGame();

    /// Run the game instance.
    public static void Run<T>() where T : SSLGame, new()
    {
        // Safely run without running-the-gun.
        using T type = new();
        Game = type;
        Game.Run();
    }
}