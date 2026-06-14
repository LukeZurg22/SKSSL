using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using MonoGameGum;
using SKSSL.ECS;
using SKSSL.ECS.Registry;
using SKSSL.Scenes;
using SKSSL.Textures;
using SKSSL.Utilities;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

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
    #region Static Fields /*Don't make too many of these.*/

    public static SSLGame Instance { get; private set; }

    internal static GraphicsDevice Graphics => Instance.GraphicsDevice;

    /// Aspect ratio to render the game.
    public static float AspectRatio => Graphics.Viewport.AspectRatio;

    /// Use static constructor for this.
    public static EngineConfig Config { get; set; } = new();

    #endregion

    #region Fields

    /// Title of game.
    public string Title => Window.Title;

    /// Total time played for this game session.
    public DateTime GameplayTime;

    /// General context of the game dictated here.
    public SceneManager SceneManager = null!;

    /// Static-instanced access for the Content Manager belonging to the active game instance.
    public readonly List<ContentManager> ContentManagers = [];

    private readonly GraphicsDeviceManager _graphicsManager;
    private readonly SpriteBatch _spriteBatch;

    private static GumService? Gum;
    private readonly InteractiveGue _currentScreenGue = new();

    public readonly ImGuiRenderer GuiRenderer;

    /// Registries and services belonging to the game.
    private readonly IServiceProvider GameServices;

    /// All content directories contained in the game folder. (E.g. game, mods ➡ etc.)
    public readonly GameContentDirectories Directories;

    // TEMP: Consider just throwing this away. Are they ever accessed outside of this class? Probably not!

    public MouseWrapper MouseHandler;

    #endregion

    /// Base constructor runs first.
    // ReSharper disable once UnusedMember.Global
    protected SSLGame() : this("SSLGame")
    {
    }

    private readonly string _gumFilePath;

    /// <summary>
    /// Constructor for SSLGame. Runs before any inheritors.
    /// </summary>
    /// <param name="title">Title of the game window.</param>
    /// <param name="contents">Additional content managers belonging to attached libraries.</param>
    protected SSLGame(string title, params ContentManager[] contents)
    {
        #region Settings

        if (string.IsNullOrEmpty(Config.GumFile))
            Log($"No gum project file in Content/Gum in {Title}, {nameof(SSLGame)} Class.", LOG.SYSTEM_WARNING);
        else // Prepend Gum root.
            _gumFilePath = Path.Combine("Gum", Config.GumFile);

        // Load settings, and based on game paths, create directories ordered by load order.
        GameSettings settings = GameSettings.Load(); // Get game settings from file.
        Directories = GetGameDirectories(settings.GamePaths);
        Directories.Sort();

        // Init w. language from settings.
        Loc.InitalizeLocalizationCulture(settings.Language);

        #endregion

        #region Monogame Usuals

        // WIP: Add IsBorderless & IsFullScreen option handling here, plus screen Width & Height if windowed.
        //  Borderless = False assumes windowed.

        Instance = this;
        Window.Title = title;
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged; // TEMP: Something tells me this doesn't work...!
        Window.IsBorderless = settings.IsBorderless; // Set to settings' borderless value.
        _graphicsManager = HandleGraphicsManager(new GraphicsDeviceManager(this), settings);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _currentScreenGue.UpdateLayout(); // UI Behaviour when dragged
        MouseHandler = new MouseWrapper(_graphicsManager);
        StyleSheet.LoadStyles();

        var services = new ServiceCollection();
        LoadServices(services);
        GameServices = services.BuildServiceProvider();

        // Assign static-access content managers.
        ContentManagers.Add(Content);
        ContentManagers.AddRange(contents);

        #endregion

        #region SSLGame Additionals

        // Display ECS status. This called after inheritors.
        Log($"ECS status: {(Config.UseECS ? "on" : "off")}");
        if (Config.UseECS)
        {
            Log($"Source generator accounted for {ComponentRegistry.Count} components:");
            // Print all registered components in a nice list. 
            StringBuilder componentTypesOutput = new();
            foreach ((string? handle, Type? type) in ComponentRegistry.RegisteredHandleComponentTypesDictionary)
            {
                componentTypesOutput.AppendLine($"\n  {handle} -> ID {ComponentRegistry.GetId(type)}");
            }

            Log(componentTypesOutput.ToString());
        }

        Log("Initializing ImGUI.");
        GuiRenderer = new ImGuiRenderer(this);
        GuiRenderer.RebuildFontAtlas();

        // If there aren't any directories, it either is a failure on behalf of the loader, or that one isn't defined.
        //  If there ever is such a case, then the entire game's folder outside of the binaries is its game directory.
        Log($"Loading {Directories.Count} Game Directories.");
        foreach (GameDirectory directory in Directories)
        {
            LoadGameDirectories(directory);
            string directoryTitle = directory.DirectoryTitle;
            if (string.IsNullOrEmpty(directoryTitle))
                directoryTitle = "root";

            Log($"...finished loading {directoryTitle} directory...");
        }

        #endregion
    }

    /// WIP: loading directories.
    ///  == Textures & Materials
    ///  == Prototypes (check ECS I guess?)
    ///  Make a breakpoint & double-check that load order is operational. Higher order = higher priority!
    private static void LoadGameDirectories(GameDirectory directory)
    {
        // Assuming there are defined directories to begin with...
        // Localization.
        if (directory.LocalizationFolder != null)
        {
            Log($"...loading {directory.DirectoryTitle} localization.");
            Loc.Load(directory.LocalizationFolder);
        }

        // Textures.
        if (directory.TexturesFolder != null)
        {
            Log($"...loading {directory.DirectoryTitle} textures.");
            new TextureLoader().Load(directory.TexturesFolder);
        }

        // Prototypes.
        if (directory.PrototypesFolder != null && Config.UseECS) // Requires ECS to be on.
        {
            Log($"...loading {directory.DirectoryTitle} prototypes.");
            Config.ContentLoader.Load(directory.PrototypesFolder); // WIP: Handle mod overrides once more.
            // TODO: Add custom bootstrapping so developer can have their own loader slotted in.
        }

        Log($"...loaded {MasterRegistryManager.Count()} prototypes.");
    }


/*
 * Methods that handle ulterior loading outside of simple Monogame stuff. Game Directories, localization, etc.
 */

    #region Utility Methods

    /// Get game directories stored in settings.
    [SuppressMessage("ReSharper", "BadChildStatementIndent")]
    private static GameContentDirectories GetGameDirectories(List<LoadPath> settings)
    {
        GameContentDirectories contentDirectories = new();

        /*
            If there are designated game paths, create Game Directories.
            No designated game paths means that a specialized dynamic one will be needed. Mods basically don't
            exist in this arrangement, but can be added later. This is the expected arrangement that a game will take.
        */
        var modifiedSettings = settings.ToList();
        if (settings.Count == 0)
        {
            contentDirectories.Add();
        }
        else
        {
            /*  Once a singular game-path is added to the list, any root-level directory would be rendered completely
             worthless. To avoid this conundrum, the specific key word "root" was allocated to check and remove. */
            if (settings.Any(d => d.Path.Contains("root")))
            {
                LoadPath rootPath = settings.First(d => d.Path.Contains("root"));
                modifiedSettings.Remove(rootPath);
                // Add root path as "officially" accepted path if provided in list. It has its own load-order!
                contentDirectories.Add("", rootPath.Order);
            }

            // Ensure that duplicates are not added!
            foreach (LoadPath gamePath in modifiedSettings)
            {
                if (!contentDirectories.Any(d => d.DirectoryTitle.Equals(gamePath.Path)))
                {
                    contentDirectories.Add(gamePath.Path, gamePath.Order);
                }
            }
        }

        return contentDirectories;
    }

    private GumProjectSave? InitializeGum()
    {
        // Initialize Gum UI Handling (Some projects may choose not to utilize Gum)
        GumProjectSave? gumProjectSave = null;
        if (string.IsNullOrEmpty(_gumFilePath))
            return gumProjectSave;
        Gum = GumService.Default;
        gumProjectSave = Gum.Initialize(this, _gumFilePath);

        return gumProjectSave;
    }

    #endregion

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

    private static GraphicsDeviceManager HandleGraphicsManager(GraphicsDeviceManager graphicsDeviceManager,
        GameSettings settings)
    {
        int monitorWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
        int monitorHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
        
        // Assign settings to heights and etc.
        if (settings.Width != -1)
            monitorWidth = settings.Width;
        if (settings.Height != -1)
            monitorWidth = settings.Height;

        // Fullscreen?
        graphicsDeviceManager.IsFullScreen = settings.IsFullScreen;

        graphicsDeviceManager.PreferredBackBufferWidth = monitorWidth; // Set preferred width
        graphicsDeviceManager.PreferredBackBufferHeight = monitorHeight; // Set preferred height
        graphicsDeviceManager.ApplyChanges();
        return graphicsDeviceManager;
    }

    protected override void Initialize()
    {
        GumProjectSave? gumSave = InitializeGum();

        SceneManager = new SceneManager(this, _graphicsManager, _spriteBatch, gumSave);
        Components.Add(SceneManager);

        if (Config.UseECS)
        {
            SystemManager.Initialize();
        }

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
        if (Config.UseECS)
        {
            SystemManager.Draw(gameTime);
        }

        Gum?.Draw(); // Draw Gum UI after game draw.
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        GameplayTime = GameplayTime.AddSeconds(gameTime.ElapsedGameTime.TotalSeconds);

        MouseWrapper.HandleForcedPosition();

        base.Update(gameTime);
        if (Config.UseECS)
        {
            SystemManager.Update(gameTime);
        }

        Gum?.Update(gameTime); // Update Gum UI after game update.
    }
}