using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameGum;

namespace SKSSL.Scenes;

public abstract class SSLGame : Game
{
    public readonly SceneManager SceneManager;

    internal readonly GraphicsDeviceManager _graphicsManager;
    internal readonly SpriteBatch _spriteBatch;

    private static GumService Gum => GumService.Default;
    private readonly InteractiveGue currentScreenGue = new();

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "Gum/SolKom.gumx"
    /// </code>
    /// </summary>
    public static string GumFile = "CHANGE_ME"; // Example: 

    protected SSLGame(string title, string gumFile = "")
    {
        Title = title;
        SceneManager = new SceneManager(this);
        _graphicsManager = HandleGraphicsDesignManager(new GraphicsDeviceManager(this));
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged;
        currentScreenGue.UpdateLayout(); // UI Behaviour when dragged

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        Content.RootDirectory = "Content";

        if (string.IsNullOrEmpty(gumFile))
            DustLogger.Log($"Provided gum project file is empty! {title}, {nameof(SSLGame)}", 3);
        else
            GumFile = gumFile;
    }

    // WARN: I have no idea how to do networking. This needs work. Set False as Default, for now.
    public bool IsNetworkSupported { get; set; } = false;
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

    public void Quit() => throw new NotImplementedException();

    public void ResetGame() => throw new NotImplementedException();

    protected override void Draw(GameTime gameTime)
    {
        SceneManager.Draw(gameTime);
        Gum.Draw();
        base.Draw(gameTime);
    }
    
    protected override void Update(GameTime gameTime)
    {
        SceneManager.Update(gameTime);
        Gum.Update(gameTime);
        base.Update(gameTime);
    }
}