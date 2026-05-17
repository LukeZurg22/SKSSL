using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using static SKSSL.DustLogger;

// ReSharper disable VirtualMemberNeverOverridden.Global

namespace SKSSL.Scenes;

/// <summary>
/// An instanced scene used by the <see cref="SceneManager"/> to track the active game state.
/// </summary>
/// <remarks>
/// A Main Menu versus active Gameplay interfaces are good examples of Scenes. Not to be confused with the theatrical.
/// </remarks>
public abstract class BaseScene
{
    /// Dedicated scene spritebatch for screen rendering.
    protected SpriteBatch _spriteBatch = null!;

    /// Graphics management passed-down from the game.
    protected GraphicsDeviceManager _graphicsManager = null!;

    /// List of UI elements this scene possesses. Used by Gum as a simple list of Element references in memory.
    // ReSharper disable once CollectionNeverUpdated.Global
    protected readonly List<FrameworkElement> _Menus = [];

    /// <summary>
    /// World definition that should be initialized with a custom variant.
    /// Allows developers to initialize world settings / data per-scene.
    /// </summary>
    /// <remarks>
    /// Gameworld is passed-through as a reference. A world does NOT need to be updated within a scene, and shouldn't.
    /// The <see cref="SceneManager"/> calls world updates.
    /// </remarks>
    public IWorld? GameWorld;

    /// <summary>
    /// Initializes game world in this scene.
    /// </summary>
    public void Initialize(GraphicsDeviceManager manager, SpriteBatch gameSpriteBatch, ref IWorld? world)
    {
        _spriteBatch = gameSpriteBatch;
        _graphicsManager = manager;
        GameWorld = world;

        if (GameWorld != null)
        {
            Log("...initializing world...");
            GameWorld.Initialize(manager); // GameWorld has its own spritebatch.
        }
    }

    /// The screens and UI elements that are being loaded in this scene.
    public virtual void LoadContent()
    {
        _Menus.ForEach(element => element.AddToRoot());
        GameWorld?.LoadContent();
    }

    /// Calls destructive actions against the game world and additional special developer-provided unload calls.
    public virtual void UnloadContent() => GameWorld?.Destroy();

    /// Per-scene Update instructions.
    public virtual void Update(GameTime gameTime) => GameWorld?.Update(gameTime);

    /// Per-scene Draw instructions.
    public virtual void Draw(GameTime gameTime) => GameWorld?.Draw(gameTime);
}