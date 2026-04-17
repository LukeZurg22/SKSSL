using Microsoft.Xna.Framework;
using SKSSL.ECS;
using static SKSSL.DustLogger;
using EventHandler = SKSSL.ECS.EventHandler;

// ReSharper disable UnusedAutoPropertyAccessor.Global
// ReSharper disable PublicConstructorInAbstractClass

namespace SKSSL.Scenes;

/// <summary>
/// Common contract for all worlds (used by SceneManager, ECSController, etc.)
/// All worlds are inherently renderable spaces, but additional rendering code is required on the developer's part.
/// This may not include menus if GumUI is used on the Screen layer.
/// </summary>
public interface IWorld
{
    /// Initializes the Game World.
    void Initialize(GraphicsDeviceManager graphics);

    /// LoadContent call to load additional content after Initialize is called.
    void LoadContent();

    /// Update calls made into the game world.
    void Update(GameTime gameTime);

    /// Draw calls made into the game world.
    void Draw(GameTime gameTime);

    /// Actions taken before the world is destroyed. Saving measures, deletions, etc.
    void Destroy();
}

/// <summary>
/// Overridable inherited dictation of how a World, its Renderable Space, and its systems.
/// <see cref="UsesECS"/> toggled override will permit automatic updating of underlying systems.
/// A "physical" virtual space or area that is rendered for gameplay. Constitutes, typically, the entire field that
/// which the user will play in. Implement this class however you see fit.
/// Add your rendering / other code within your World class.
/// </summary>
public abstract class BaseWorld : IWorld
{
    /// <summary>
    /// Event handler for this world's systems.
    /// </summary>
    public readonly EventHandler Events = new();
    
    protected bool IsInitialized { get; private set; }

    /// Most worlds use ECS — this depends on overall dictation. If ECS is enabled,
    /// then a world can be forcefully disconnected per its definition. 
    /// <value>SSLGame.<see cref="SSLGame.UseECS"/></value>
    protected virtual bool UsesECS => SSLGame.UseECS;

    internal GraphicsDeviceManager _graphics { get; private set; } = null!;

    /// ECS controller unique to this world instance. Left null of no ECS controller.
    public ECSController ECS { get; } = null!;

    /// Calls ECS Init() (if enabled)
    protected BaseWorld()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        // Enable ECS if toggled-on.
        if (!UsesECS) return;
        ECS = new ECSController(this);
    }

    /// Calls Spacial Initializations as base class method.
    public virtual void Initialize(GraphicsDeviceManager graphics)
    {
        _graphics = graphics;

        if (!UsesECS) return;
        Log("...initializing ECS...");
        ECS.Initialize(graphics);
    }

    /// <inheritdoc cref="IWorld.LoadContent"/>
    public virtual void LoadContent()
    {
    }

    /// <inheritdoc cref="IWorld.Update"/>
    public virtual void Update(GameTime gameTime)
    {
        if (!UsesECS) return;
        ECS.Update(gameTime);
    }

    /// <inheritdoc cref="IWorld.Draw"/>
    public virtual void Draw(GameTime gameTime)
    {
        if (!UsesECS) return;
        ECS.Draw(gameTime);
    }

    /// <inheritdoc cref="IWorld.Destroy"/>
    public virtual void Destroy()
    {
        IsInitialized = false;
        
        if (!UsesECS) return;
        ECS.Destroy();
    }
}