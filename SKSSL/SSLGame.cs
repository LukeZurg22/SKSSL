using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using FmodAudio.Base;
using FmodForFoxes;
using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using MonoGameGum;
using SKSSL.Console;
using SKSSL.ECS;
using SKSSL.Extensions;
using SKSSL.Scenes;
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
public class SSLGame : Game
{
    #region Static Fields /*Don't make too many of these.*/

    public static SSLGame Instance { get; private set; }
    internal static GraphicsDevice Graphics => Instance.GraphicsDevice;

    /// Aspect ratio to render the game.
    public static float AspectRatio => Graphics.Viewport.AspectRatio;

    /// Static getter for Instance.Config.
    public static EngineConfig Engine => Instance.Config;

    /// Access the general content loader.
    public static IGameLoader PrototypeLoader => Engine.PrototypeLoader;

    public static bool UsesECS => Engine.UseECS;
    public static TextureManager TextureManager => Instance.Services.GetService<TextureManager>();

    public static SoundManager SoundManager => Instance.Services.GetService<SoundManager>();

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

    /// All content directories contained in the game folder. (E.g. game, mods ➡ etc.)
    public readonly GameContentDirectories Directories;

    // TEMP: Consider just throwing this away. Are they ever accessed outside of this class? Probably not!
    public MouseWrapper MouseHandler;

    public readonly GameSettings Settings;

    /// Configurable engine configuration assigned from the Developer's view.
    public readonly EngineConfig Config;

    // ReSharper disable once UnusedAutoPropertyAccessor.Global
    public Checksum Checksum { get; private set; }

    private readonly INativeFmodLibrary _nativeLibrary;

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
    protected SSLGame(string title)
    {
        Instance = this; // MonoGame priority assignments.
        Window.Title = title.IsNullOrEmpty() ? Title : "SKSSL";
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged; // TEMP: Something tells me this doesn't work...!
        // ReSharper disable once VirtualMemberCallInConstructor
        Config = BuildEngineConfig(); // This virtual call is desired, as the override acting first is intended.
        _nativeLibrary = new DesktopNativeFmodLibrary();

        #region Settings

        if (string.IsNullOrEmpty(Config.GumFile))
        {
            //@formatter:off
            Log($"No gum project file in Content/Gum for {Title}, {nameof(SSLGame)} Class.", LOG.SYSTEM_WARNING);
            //@formatter:on
        }
        else
        {
            _gumFilePath = Path.Combine("Gum", Config.GumFile); // Prepend Gum root.
        }

        // Load settings, and based on game paths, create directories ordered by load order.
        (GameSettings Settings, List<LoadPath> Paths) load = GameSettings.Load(); // Get game settings from file.
        Settings = load.Settings;
        Directories = GetGameDirectories(load.Paths);
        Directories.Sort();

        // Init w. language from settings.
        Loc.InitalizeLocalizationCulture(Settings.Language);

        #endregion

        #region Monogame Usuals

        Window.IsBorderless = Settings.IsBorderless; // Set to settings' borderless value.
        _graphicsManager = HandleGraphicsManager(new GraphicsDeviceManager(this), load.Settings);
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _currentScreenGue.UpdateLayout(); // UI Behaviour when dragged
        MouseHandler = new MouseWrapper(_graphicsManager);
        StyleSheet.LoadStyles();

        // Assign static-access content managers.
        ContentManagers.Add(Content);

        // ReSharper disable once VirtualMemberCallInConstructor
        LoadServices();

        #endregion

        #region SSLGame Additionals

        StringBuilder ecs_SB = new();

        // Display ECS status. This called after inheritors.
        ecs_SB.AppendLine($"ECS status: {(Config.UseECS ? "on" : "off")}");
        if (Config.UseECS)
        {
            ecs_SB.AppendLine($"Source generator found {ComponentRegistry.Count} components:");

            // Print all registered components in a nice list. 
            foreach ((string? handle, Type? type) in ComponentRegistry.RegisteredHandleComponentTypesDictionary)
                ecs_SB.AppendLine($"{handle} ({ComponentRegistry.GetId(type)})");
        }

        // Spit out component logging.
        Log(ecs_SB.ToString());

        Log("Initializing ImGUI.");
        GuiRenderer = new ImGuiRenderer(this);
        GuiRenderer.RebuildFontAtlas();

        // If there aren't any directories, it either is a failure on behalf of the loader, or that one isn't defined.
        //  If there ever is such a case, then the entire game's folder outside the binaries is its game directory.
        Log($"Loading {Directories.Count} Game Directories.");
        foreach (GameDirectory directory in Directories)
            directory.Load();

        #endregion
    }

    /// <summary>
    /// Creates the engine configuration before the remainder of the engine is initialized.
    /// Do not access derived instance state from this method.
    /// </summary>
    public virtual EngineConfig BuildEngineConfig() => new();

    #region Utility Methods

    /*
     * Methods that handle ulterior loading outside of simple Monogame stuff. Game Directories, localization, etc.
     */


    /// Get game directories stored in settings.
    [SuppressMessage("ReSharper", "BadChildStatementIndent")]
    private static GameContentDirectories GetGameDirectories(List<LoadPath> paths)
    {
        GameContentDirectories contentDirectories = new();

        /*
            If there are designated game paths, create Game Directories.
            No designated game paths means that a specialized dynamic one will be needed. Mods basically don't
            exist in this arrangement, but can be added later. This is the expected arrangement that a game will take.
        */
        // Load all paths except the ones that aren't enabled.
        var modifiedSettings = paths.Except(paths.FindAll(p => p.Enabled == false))
            .ToList();

        const string root = "root";
        if (paths.Count == 0)
        {
            contentDirectories.Add(root);
            return contentDirectories;
        }

        //  Once at least one path is in the list, any root-level directory would be rendered completely-
        //   -worthless. To avoid this conundrum, the specific key word "root" was allocated to check and remove.
        if (paths.Any(path => path.Path.Contains(root)))
        {
            var rootPaths = paths.FindAll(path => path.Path.Contains(root));

            // Add root path as "officially" accepted path if provided in list. It has its own load-order!
            // Use the first and only one.
            contentDirectories.Add(root, rootPaths[0].Order);

            // Remove root path duplicates.
            foreach (LoadPath rootPath in rootPaths)
                modifiedSettings.Remove(rootPath); // Remove the roots.
        }

        // Ensure that duplicates are not added!
        foreach (LoadPath gamePath in modifiedSettings)
        {
            // Game paths set to -1 are ignored.
            if (gamePath.Order == -1)
                continue;

            if (!contentDirectories.Any(d => d.DirectoryTitle.Equals(gamePath.Path)))
                contentDirectories.Add(gamePath.Path, gamePath.Order);
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
    /// <code>services.AddSingleton&lt;ExampleRegistry&gt;();</code>
    /// <remarks>
    /// Add game services to an override method. Unless you replace the registr(y|ies) somehow,
    /// you MUST add a base.LoadServices() call.
    /// </remarks>
    protected virtual void LoadServices()
    {
        Services.AddService(new TextureManager());
        Services.AddService(new SoundManager());
    }

    #region Graphics & Window Management

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

        // Assign settings to heights etc. Keeps the default monitor size, otherwise.
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

    #endregion

    protected override void Initialize()
    {
        GumProjectSave? gumSave = InitializeGum();

        SceneManager = new SceneManager(this, _graphicsManager, _spriteBatch, gumSave);
        Components.Add(SceneManager);

        if (Settings.SKSSLConsoleEnabled)
        {
            var gameConsole =
                new GameConsole(this, new SpriteBatch(GraphicsDevice), GameConsoleOptions.SolKomDefault());
            Components.Add(new GameConsoleComponent(gameConsole, this, _spriteBatch));
        }

        if (Config.UseECS)
        {
            SystemManager.Initialize();
        }

        //FmodManager.Init(_nativeLibrary, FmodInitMode.Core, "Content");
        Checksum = Checksum.Generate(this); // Now the game is mostly initialized, generate a checksum.
        SoundManager.Enabled = true;
        base.Initialize(); // Continue
    }

    protected override void UnloadContent()
    {
        //FmodManager.Unload();
        SoundManager.Enabled = false;
    }

    /// Quits the game.
    public static void Quit() =>
        throw new NotImplementedException("Quit is not implemented, really. Let's crash, instead.");

    /// Resets the game.
    public static void ResetGame() =>
        throw new NotImplementedException("ResetGame is not implemented, really. Let's crash, instead.");

    protected override void Draw(GameTime gameTime)
    {
        base.Draw(gameTime); // Draw game first.
        if (Config.UseECS)
            SystemManager.Draw(gameTime);
        Gum?.Draw(); // Draw Gum UI after game draw.
    }

    /// <inheritdoc />
    protected override void Update(GameTime gameTime)
    {
        FmodManager.Update();
        GameplayTime = GameplayTime.AddSeconds(gameTime.ElapsedGameTime.TotalSeconds);
        MouseWrapper.HandleForcedPosition();
        base.Update(gameTime);
        if (Config.UseECS)
            SystemManager.Update(gameTime);
        Gum?.Update(gameTime); // Update Gum UI after game update.
        SoundManager.Update(gameTime); // Update sound manager. Hope it's there and not gone!
    }
}