namespace SKSSL.Scenes;

public static class GameManager
{
    public static SSLGame Game { get; private set; } = null!;
    public static string Title => Game.Title;
    public static float AspectRatio => Game.GraphicsDevice.Viewport.AspectRatio;
    public static bool IsNetworkSupported => Game.IsNetworkSupported;

    public static void Exit()
    {
        // Safely exit without suicidal tendencies.
        SSLGame game = Game;
        game.Quit();
        game.Exit();
    }
    public static void ResetGame() => Game.ResetGame();

    public static void Run<T>() where T : SSLGame, new()
    {
        // Safely run without running-the-gun.
        using T type = new();
        Game = type;
        type.Run();
    }
}