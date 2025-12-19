using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;
using SKSSL.Scenes;

namespace KBSL.Types;

public abstract class BaseGame : Game
{
    public readonly BaseSceneManager SceneManager;

    private readonly GraphicsDeviceManager _graphicsManager;
    private readonly SpriteBatch _spriteBatch;

    private static GumService Gum => GumService.Default;
    private readonly InteractiveGue currentScreenGue = new();

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "Gum/SolKom.gumx"
    /// </code>
    /// </summary>
    public static string GumFile = "CHANGE_ME"; // Example: 

    protected BaseGame(string title, string gumFile = "")
    {
        Title = title;
        SceneManager = new BaseSceneManager(this);
        _graphicsManager = HandleGraphicsDesignManager(new GraphicsDeviceManager(this));
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged;
        currentScreenGue.UpdateLayout(); // UI Behaviour when dragged

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Content.RootDirectory = "Content";

        GumFile = gumFile;
    }

    public bool IsNetworkSupported { get; set; }
    public string Title { get; set; }

    /// <summary>
    /// Accommodates for when the user readjusts the UI dimensions.
    /// </summary>
    private void HandleClientSizeChanged(object? _, EventArgs e)
    {
        GraphicalUiElement.CanvasWidth = _graphicsManager.GraphicsDevice.Viewport.Width;
        GraphicalUiElement.CanvasHeight = _graphicsManager.GraphicsDevice.Viewport.Height;
    }

    private static GraphicsDeviceManager HandleGraphicsDesignManager(GraphicsDeviceManager graphicsDeviceManager)
    {
        var monitorWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        var monitorHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        graphicsDeviceManager.PreferredBackBufferWidth = monitorWidth; // Set preferred width
        graphicsDeviceManager.PreferredBackBufferHeight = monitorHeight; // Set preferred height
        graphicsDeviceManager.ApplyChanges();
        return graphicsDeviceManager;
    }

    protected override void Initialize()
    {
        // Initialize Gum UI Handling (Some projects may choose not to utilize Gum)
        GumProjectSave? gumSave = null;
        if (!string.IsNullOrEmpty(GumFile)) gumSave = Gum.Initialize(this, GumFile);
        SceneManager.Initialize(_graphicsManager, _spriteBatch, gumSave); // Initialize Scene Manager

        // Continue
        base.Initialize();
    }

    public void Quit()
    {
        throw new NotImplementedException();
    }

    public void ResetGame()
    {
        throw new NotImplementedException();
    }
}