using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using MonoGameGum;
using SKSSL.Console;
using SKSSL.ECS;
using SKSSL.ECS.Registry;
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
public abstract class SSLGame : Game
{
    #region Static Fields /*Don't make too many of these.*/

    public static SSLGame Instance { get; private set; }

    internal static GraphicsDevice Graphics => Instance.GraphicsDevice;

    /// Aspect ratio to render the game.
    public static float AspectRatio => Graphics.Viewport.AspectRatio;

    public static TextureRegistry TextureRegistry => Instance.Services.GetService<TextureRegistry>();

    /// Access the general content loader.
    public static IGameLoader PrototypeLoader => Instance.Config.PrototypeLoader;

    public static bool UsesECS => Instance.Config.UseECS;
    
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

        Log($"Loading {Directories.Count} Game Directories.");
        LoadGameDirectories();

        #endregion
    }

    /// Overridable engine configuration.
    public virtual EngineConfig BuildEngineConfig() => new();

    /// If there aren't any directories, it either is a failure on behalf of the loader, or that one isn't defined.
    ///  If there ever is such a case, then the entire game's folder outside of the binaries is its game directory.
    private void LoadGameDirectories()
    {
        foreach (GameDirectory directory in Directories)
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
                // The developer can bootstrap their own texture loader by adjusting the Engine Config.
                Log($"...loading {directory.DirectoryTitle} textures.");
                Config.TextureLoader.Load(directory.TexturesFolder);
            }

            // Prototypes.
            if (directory.PrototypesFolder != null) // Requires ECS to be on.
            {
                //@formatter:off
                if (!Config.UseECS) Log($"Cannot load prototypes from {directory} folder. ECS is not Enabled!", LOG.SYSTEM_WARNING);
                else
                {
                    // The developer can bootstrap their own prototype loader by adjusting the Engine Config.
                    Log($"...loading {directory.DirectoryTitle} prototypes.");
                    Config.PrototypeLoader.Load(directory.PrototypesFolder);
                    Log($"...loaded {MasterRegistryManager.Count()} prototypes.");
                }
                //@formatter:on
            }

            // WIP: Sounds.
            // if (directory.SoundsFolder != null)
            // {
            //      Log($"...loading {directory.DirectoryTitle} sounds.");
            //      Config.SoundLoader.Load(directory.SoundsFolder);
            // }
            string directoryTitle = directory.DirectoryTitle;
            if (string.IsNullOrEmpty(directoryTitle))
                directoryTitle = "root";
            Log($"...finished loading {directoryTitle} directory...");
        }
    }


/*
 * Methods that handle ulterior loading outside of simple Monogame stuff. Game Directories, localization, etc.
 */

    #region Utility Methods

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
    /// Add game services to an override method. Unless you replace the registr(y|ies) somehow, add a base call.
    /// </remarks>
    protected virtual void LoadServices()
    {
        Services.AddService(new TextureRegistry());
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
            SystemManager.Initialize();

        base.Initialize(); // Continue
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
        GameplayTime = GameplayTime.AddSeconds(gameTime.ElapsedGameTime.TotalSeconds);
        MouseWrapper.HandleForcedPosition();
        base.Update(gameTime);
        if (Config.UseECS)
            SystemManager.Update(gameTime);
        Gum?.Update(gameTime); // Update Gum UI after game update.
    }
}