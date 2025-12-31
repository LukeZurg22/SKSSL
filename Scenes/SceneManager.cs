using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using MonoGameGum;
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable UnusedMember.Global

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

public class SceneManager
{
    protected SpriteBatch _spriteBatch;
    protected GraphicsDeviceManager _graphicsManager;
    protected GumProjectSave? _gumProjectSave;
    protected BaseScene? _currentScene;
    public static SSLGame Game { get; private set; } = null!;

    public SceneManager(SSLGame game)
    {
        Game = game;
        _spriteBatch = game._spriteBatch;
        _graphicsManager = game._graphicsManager;
        _currentScene = null;
    }

    /// <summary>
    /// Checks if "GumService.Default.Root.Children" is not Null, and if not, clears them.
    /// </summary>
    public static void ClearScreens()
    {
        if (GumService.Default.Root.Children != null)
            GumService.Default.Root.Children.Clear();
    }
    
    public void Initialize(
        GraphicsDeviceManager graphicsManager,
        SpriteBatch spriteBatch,
        GumProjectSave? gumProjectSave)
    {
        _graphicsManager = graphicsManager;
        _spriteBatch = spriteBatch;
        _gumProjectSave = gumProjectSave;
    }
    
    public static void LoadScreen<T>() where T : new()
    {
        if (typeof(T).BaseType != typeof(FrameworkElement))
            return;

        ClearScreens();

        T screen = new();
        (screen as FrameworkElement ??
         throw new InvalidOperationException("The screen didn't cast correctly on load"))
            .AddToRoot();
    }

    public void SwitchScene(BaseScene newScene)
    {
        MediaPlayer.Stop(); // Stop The Music
        _currentScene?.UnloadContent(); // UniqueUnloadContent the current scene

        _currentScene = newScene; // Switch to the new scene
        _currentScene.Initialize(Game, _graphicsManager, _spriteBatch, _gumProjectSave, world: null); // Initialize the Scene

        _currentScene.LoadContent(); // Load the new scene content
    }

    public void Draw(GameTime gameTime) => _currentScene?.Draw(gameTime);
    public void Update(GameTime gameTime) => _currentScene?.Update(gameTime);
}