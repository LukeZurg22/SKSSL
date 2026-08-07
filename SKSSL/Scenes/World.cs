using Microsoft.Xna.Framework;
using SKSSL.ECS;
using EventHandler = SKSSL.ECS.EventHandler;

// ReSharper disable UnusedMemberInSuper.Global
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

    // ReSharper disable once UnusedParameter.Global
    /// Update calls made into the game world.
    void Update(GameTime gameTime);

    // ReSharper disable once UnusedParameter.Global
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
public class World : IWorld
{
    /// <summary>
    /// Event handler for this world's systems.
    /// </summary>
    public readonly EventHandler Events = new();

    /// Most worlds use ECS — this depends on overall dictation. If ECS is enabled,
    /// then a world can be forcefully disconnected per its definition. 
    protected static bool UsesECS => SSLGame.Config.UseECS;

    /// Graphics management embedded in this world.
    public GraphicsDeviceManager Graphics { get; private set; }

    /// ECS controller unique to this world instance..
    /// Manages  all active entities in this world.
    public readonly EntityManager EntityManager = new();

    // / Global statistics list. // TODO: Working on making this viable for "global" statistics.
    //public readonly StatisticsList StatisticsList = new();
    
    /// Calls ECS Init() (if enabled)
    protected internal World()
    {
        // ReSharper disable once VirtualMemberCallInConstructor
        // Enable ECS if toggled-on.
        if (!UsesECS) return;
        Log("...initializing world ECS...");
        EntityManager = new EntityManager();
    }

    /// Calls Spacial Initializations as base class method.
    public virtual void Initialize(GraphicsDeviceManager graphics)
    {
        Graphics = graphics;
    }

    /// <inheritdoc cref="IWorld.LoadContent"/>
    public virtual void LoadContent()
    {
    }

    /// <inheritdoc cref="IWorld.Update"/>
    public virtual void Update(GameTime gameTime)
    {
        /*
         * Entity definitions come equipped with virtual Update & Draw methods that can be overwritten. They will do
         * nothing on their own, but if for some reason you feel like overriding this Update call and overriding the
         * consensus entity-type with your own calls- you can do that.
         */
        EntityManager.Update(gameTime);
    }

    /// <inheritdoc cref="IWorld.Draw"/>
    public virtual void Draw(GameTime gameTime)
    {
        /*
         * Entity definitions come equipped with virtual Update & Draw methods that can be overwritten. They will do
         * nothing on their own, but if for some reason you feel like overriding this Draw call and overriding the
         * consensus entity-type with your own calls- you can do that.
         * It Looks something like this:
            // foreach (var entity in EntityManager.AllEntities) entity.Update(gameTime);
         */
    }

    /// <inheritdoc cref="IWorld.Destroy"/>
    public virtual void Destroy()
    {
        if (!UsesECS) return;
        EntityManager.DestroyAll();
    }
}