using System.Diagnostics.CodeAnalysis;
using Gum.DataTypes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;
using MonoGameGum;
using static SKSSL.DustLogger;

namespace SKSSL.Scenes;

/// Manages all screen content in an SKSSL-derived Game, whether it be Menus or active scenes. Not necessarily for
/// theatrical scene management. See below for adding GUM UI Overlays.
/// <code>
/// -constructor-
/// {
///     var menu = new MyMenu();
///     _Menus.Add(menu)
/// }
/// </code>
public class SceneManager : DrawableGameComponent
{
    /// Active Gum UI project save for UI handling.
    protected readonly GumProjectSave? _gumProjectSave;

    private readonly SpriteBatch _gameMainSpriteBatch;
    private readonly GraphicsDeviceManager _graphicsManager;

    /// <summary>
    /// World definition that should be initialized with a custom variant.
    /// Allows developers to initialize world settings / data per-scene.
    /// <remarks>May need improvement later.</remarks>
    /// </summary>
    protected internal IWorld? CurrentWorld;

    /// Active in-use game scene. Class-type specific.
    protected BaseScene? _currentScene;

    /// Constructor for Scene Manager used by <see cref="SSLGame"/> to manage active game scenes.
    public SceneManager(
        Game game,
        GraphicsDeviceManager graphics,
        SpriteBatch gameMainSpriteBatch,
        GumProjectSave? gumSave)
        : base(game)
    {
        _gameMainSpriteBatch = gameMainSpriteBatch;
        _graphicsManager = graphics;
        _currentScene = null;
        _gumProjectSave = gumSave;
    }

    /// <summary>
    /// Checks if "GumService.Default.Root.Children" is not Null, and if not, clears them.
    /// </summary>
    public static void ClearScreens()
    {
        if (GumService.Default.Root.Children != null)
            GumService.Default.Root.Children.Clear();
    }

    /// <summary>
    /// Switches scene to new scene based on provided scene type.
    /// </summary>
    /// <typeparam name="TScene"></typeparam>
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public void SwitchScene<TScene>() where TScene : BaseScene, new() => SwitchScene(new TScene());

    /// <inheritdoc cref="SwitchScene{TScene}"/> Utilizes instance instead of generic type.
    public void SwitchScene(BaseScene scene)
    {
        Log($"Switching to scene {scene.GetType().Name}.");
        var stopwatch = System.Diagnostics.Stopwatch.StartNew(); // Timing the load.

        Log("...clearing screens...");
        // Clear everything before creating new scene.
        MediaPlayer.Stop(); // Stop The Music
        ClearScreens(); // Clear old screens.

        // Force empty constructor of new scene. Scenes aren't instantiated and stored elsewhere, they're created here.

        Log("...unloading previous scene...");
        _currentScene?.UnloadContent(); // UniqueUnloadContent the current scene

        Log($"...creating new scene...");
        _currentScene = scene; // Switch to the new scene

        // Allow scenes to override current world.
        if (_currentScene.GameWorld != null) CurrentWorld = _currentScene.GameWorld;

        // Initialize the Scene
        Log("...initializing scene...");
        _currentScene.Initialize(_graphicsManager, _gameMainSpriteBatch, ref CurrentWorld);

        Log("...loading additional scene content...");
        _currentScene.LoadContent(); // Load the new scene content

        stopwatch.Stop(); // Stop the timer.
        Log($"Scene load complete by {stopwatch.ElapsedMilliseconds}ms");
    }
    
    /// Calls Draw Methods on Current Scene, which innately sends a draw call on the world.
    public override void Draw(GameTime gameTime) => _currentScene?.Draw(gameTime);

    /// Calls Update Methods on Current Scene, which innately sends an update call on the world.
    public override void Update(GameTime gameTime) => _currentScene?.Update(gameTime);
}