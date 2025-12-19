using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using MonoGameGum;
// ReSharper disable ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
// ReSharper disable UnusedMember.Global

namespace SKSSL.Scenes;

public class BaseSceneManager
{
    protected SpriteBatch _spriteBatch;
    protected GraphicsDeviceManager _graphicsManager;
    protected GumProjectSave? _gumProjectSave;
    
    protected BaseGameScene _currentScene;
    protected readonly Game _game;

    public BaseSceneManager(Game game) => _game = game;

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

    protected void SwitchScene(BaseGameScene newScene)
    {
        MediaPlayer.Stop(); // Stop The Music
        _currentScene?.UnloadContent(); // UniqueUnloadContent the current scene

        _currentScene = newScene; // Switch to the new scene
        _currentScene.Initialize(_game, _graphicsManager, _spriteBatch, _gumProjectSave); // Initialize the Scene

        _currentScene.LoadContent(); // Load the new scene content
    }

    protected void Draw(GameTime gameTime, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
    {
        _currentScene?.Draw(gameTime, graphicsDevice, spriteBatch);
    }

    protected void Update(GameTime gameTime)
    {
        _currentScene?.Update(gameTime);
    }
}