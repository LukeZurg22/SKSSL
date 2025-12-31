using Gum.DataTypes;
using Gum.Forms.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
// ReSharper disable NotAccessedField.Local

#pragma warning disable CS0169 // Field is never used

// ReSharper disable CollectionNeverQueried.Local

// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable VirtualMemberNeverOverridden.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace SKSSL.Scenes;

public abstract class BaseScene
{
    protected Game _game;
    protected SpriteBatch _spriteBatch;
    protected GraphicsDeviceManager _graphicsManager;
    internal GumProjectSave? _gumProjectSave;

    protected readonly List<FrameworkElement> _Menus = [];
    
    /// <summary>
    /// World definition that should be initialized with a custom variant.
    /// </summary>
    private BaseWorld _world;
    protected BaseScene(BaseWorld world) => _world = world;

    public void Initialize(
        Game game,
        GraphicsDeviceManager graphicsManager,
        SpriteBatch spriteBatch,
        GumProjectSave? gumProjectSave,
        BaseWorld? world)
    {
        _game = game;
        _graphicsManager = graphicsManager;
        _spriteBatch = spriteBatch;
        _gumProjectSave = gumProjectSave;
        
        // This is such that the world isn't overwritten by initializing a new scene.
        if (world != null)
            _world = world;
    }

    protected abstract void LoadScreens();
    protected abstract void UniqueLoadContent();

    public virtual void LoadContent()
    {
        LoadScreens();
        UniqueLoadContent();
    }

    public void UnloadContent()
    {
        SceneManager.ClearScreens();
        UniqueUnloadContent();
    }

    protected abstract void UniqueUnloadContent();

    public virtual void Update(GameTime gameTime)
    {
    }

    public virtual void Draw(GameTime gameTime)
    {
    }
}