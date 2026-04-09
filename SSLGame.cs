using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using MonoGameGum;
using SKSSL.ECS;
using SKSSL.Localization;
using SKSSL.Scenes;
using static SKSSL.DustLogger;

// ReSharper disable ConvertToConstant.Global
// ReSharper disable CollectionNeverQueried.Global
// ReSharper disable FieldCanBeMadeReadOnly.Global
// ReSharper disable NotAccessedField.Global
// ReSharper disable VirtualMemberCallInConstructor
// ReSharper disable NotAccessedField.Local

namespace SKSSL;

/// <summary>
/// Game Instances should inherit this class to have Gum and other systems automatically initialized.
/// <code>
/// override Initialize() {
/// base.Initialize(); // Naturally!
/// GameLoader.Register(...); // &lt;- Registering Game Loaders
/// }
/// </code>
/// Registering Game factories, loaders and such, such as anything that inherits BaseRegistry or <see cref="Loc"/>,
/// is incredibly important as these are the loaders that will LOAD the game's content.
/// </summary>
public abstract class SSLGame : Game
{
    #region Fields

    /// Title of game window.
    public string Title => Window.Title;

    /// Total time played for this game session.
    public static DateTime GameplayTime;

    /// Ultimate toggle to use ECS service. Enable this at project initialization.
    /// To use, add the following to the game class inheriting SSLGame:
    /// <code>
    /// static MyGameClass() => UseECS = true;
    /// </code>
    public static bool UseECS = false;

    /// General context of the game dictated here.
    public static SceneManager SceneManager = null!;

    /// Static-instanced access for the Content Manager belonging to the active game instance.
    public static readonly List<ContentManager> ContentManagers = [];

    private readonly GraphicsDeviceManager _graphicsManager;
    private readonly SpriteBatch _spriteBatch;

    private static GumService Gum => GumService.Default;
    private readonly InteractiveGue _currentScreenGue = new();

    /// Registries and services belonging to the game.
    private readonly IServiceProvider GameServices;

    /// <summary>
    /// An array of Tuple paths assigned to an ID. These are loaded into the game's pather, and should
    /// NEVER change. General examples include game texture and yaml prototypes folders.
    /// </summary>
    protected abstract (string id, string path)[] StaticPaths { get; }

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "Gum/SolKom.gumx"
    /// </code>
    /// </summary>
    public static string GumFile = "CHANGE_ME";

    /// All content directories contained in the game folder. (E.g. game, mods ➡ etc.)
    public readonly IEnumerable<GameContentDirectory> GameContentDirectories;

    #endregion

    private readonly ImGuiRenderer GuiRenderer;
    
    /// <remarks>
    /// In order to Spawn, Remove, or generally interact with entities in an ECS, a context is required. This context
    /// varies between scenes.
    /// //WARN: In the system's current limitations, it is difficult to interact with entities
    ///    across different scenes.
    /// </remarks>
    /// <returns>Scene Manager's Current World's Entity Context.</returns>
    public static EntityContext? ECS()
    {
        if (!UseECS)
        {
            Log("Failed to get Entity Context because ECS is not enabled.", LOG.SYSTEM_ERROR);
            return null;
        }

        if (SceneManager.CurrentWorld is not BaseWorld world)
        {
            Log("Failed to get Entity Context from current (null) world in Scene Manager!", LOG.SYSTEM_WARNING);
            return null;
        }

        if (world.ECS is null)
        {
            Log("Failed to get Entity Context for a (null) ECS Controller!", LOG.SYSTEM_WARNING);
            return null;
        }

        var entityContext = new EntityContext(world.ECS);
        return entityContext;
    }

    /// <summary>
    /// Constructor for SSLGame.
    /// </summary>
    /// <param name="title">Title of the game window.</param>
    /// <param name="gumFile">Gum Interface File</param>
    /// <param name="contents">Additional content managers belonging to attached libraries.</param>
    protected SSLGame(string title, string gumFile = "", params ContentManager[] contents)
    {
        Window.Title = title;
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged;
        _graphicsManager = HandleGraphicsDesignManager(new GraphicsDeviceManager(this));
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _currentScreenGue.UpdateLayout(); // UI Behaviour when dragged

        if (string.IsNullOrEmpty(gumFile))
            Log($"Provided gum project file is empty! {title}, {nameof(SSLGame)}", LOG.SYSTEM_WARNING);
        else
            GumFile = gumFile;

        var services = new ServiceCollection();
        LoadServices(services);
        GameServices = services.BuildServiceProvider();

        // Assign static-access content managers.
        ContentManagers.Add(Content);
        ContentManagers.AddRange(contents);

        // Initialize all static paths, which the developer must have defined!
        // Includes load-order implementation. Higher values override lower values.
        // TODO: Add a way to change load order priorities in game directories. Likely requires a file? Master file?
        //  A file per-game folder means version mismatches per file change that breaks every update.
        //  Ergo, a master file may be the best solution.
        var gameDirectories = StaticGameLoader.GetAllGameDirectories();
        GameContentDirectories = gameDirectories.OrderBy(d => d.LoadOrder).ToList();

        // Display ECS status. This constructor is called after inheritors.
        Log($"ECS status: {(UseECS ? "on" : "off")}");
        if (UseECS)
        {
            // Initializing component registry before anything else. 
            Log("Initializing components.");
            ComponentRegistry.Initialize();
        }

        // Load Static Game Content
        Log("Initializing static paths.");
        StaticGameLoader.Initialize(StaticPaths);
        StaticGameLoader.Load(path => StaticGameLoader.GPath(path));
        
        Log("Initializing ImGUI.");
        GuiRenderer = new ImGuiRenderer(this);
        GuiRenderer.RebuildFontAtlas();
    }

    /// <summary>
    /// Loads programmer-provided game services and registries.
    /// </summary>
    /// <param name="services"></param>
    /// <code>services.AddSingleton&lt;ExampleRegistry&gt;();</code>
    protected virtual void LoadServices(ServiceCollection services)
    {
        // Add game services to override method here.
    }

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

    /// <summary>
    /// For custom <see cref="StaticGameLoader"/>s, you MUST initialize them before the base.Initialize() an inheritance
    /// level above this class.
    /// </summary>
    protected override void Initialize()
    {
        // Initialize Gum UI Handling (Some projects may choose not to utilize Gum)
        GumProjectSave? gumSave = null;
        if (!string.IsNullOrEmpty(GumFile)) gumSave = Gum.Initialize(this, GumFile);
        SceneManager = new SceneManager(this, _graphicsManager, _spriteBatch, gumSave);
        Components.Add(SceneManager);

        // Continue
        base.Initialize();
    }

    /// Quits the game.
    public static void Quit() =>
        throw new NotImplementedException("Quit is not implemented, really. Let's crash, instead.");

    /// Resets the game.
    public static void ResetGame() =>
        throw new NotImplementedException("ResetGame is not implemented, really. Let's crash, instead.");

    /// <inheritdoc />
    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime);
        Gum.Draw(); // Draw Gum UI after game draw.
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        GameplayTime = GameplayTime.AddSeconds(gameTime.ElapsedGameTime.TotalSeconds);
        base.Update(gameTime);
        Gum.Update(gameTime); // Update Gum UI after game update.
    }
}