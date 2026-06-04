using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Gum.DataTypes;
using Gum.Wireframe;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.ImGuiNet;
using MonoGameGum;
using SKSSL.ECS;
using SKSSL.Scenes;
using SKSSL.Textures;
using SKSSL.Utilities;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

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
    /*
     * Use static constructor for these.
     */

    #region Engine Config

    /// Ultimate toggle to use ECS service. Enable this at project initialization.
    /// To use, add the following to the game class inheriting SSLGame:
    /// <code>
    /// static MyGameClass() => UseECS = true;
    /// </code>
    public static bool UseECS = false;

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "Gum/SolKom.gumx"
    /// </code>
    /// </summary>
    public static string GumFile = "CHANGE_ME";

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

    // TEMP: Consider content managers into contentdirectories wrapper.

    /// All content directories contained in the game folder. (E.g. game, mods ➡ etc.)
    public readonly GameContentDirectories Directories;


    // TEMP: Consider just throwing this away. Are they ever accessed outside of this class? Probably not!

    public MouseWrapper MouseHandler;

    #endregion

    /// <remarks>
    /// In order to Spawn, Remove, or generally interact with entities in an ECS, a context is required. This context
    /// varies between scenes.
    /// </remarks>
    /// <returns>Scene Manager's Current World's Entity Context.</returns>
    public static EntityContext ECS(BaseWorld? world = null)
    {
        string message;

        // If not using ECS, then why? Throw an error!
        if (!UseECS)
        {
            message = "Failed to get Entity Context because ECS is not enabled.";
            Log(message, LOG.SYSTEM_ERROR, outputToFile: true);
            throw new SettingsException(message);
        }

        // If the scene manager has a world, then use that world instead of the provided one if this is null.
        if (world == null && GameManager.Game.SceneManager.CurrentWorld is BaseWorld res)
        {
            world ??= res; // Reassign world.
        }

        // Final check to validate that the world (and its ECS) is functioning.
        if (world?.ECS is null)
        {
            message = "Failed to get Entity Context from null world or null World ECS!";
            Log(message, LOG.SYSTEM_ERROR, outputToFile: true);
            throw new Exception(message);
        }

        // Return the latest & greatest entity context!
        // Do NOT instantiate a blank-constructor EntityContext here! It will cause an infinite loop of ECS() calls!
        var entityContext = new EntityContext(world);
        return entityContext;
    }

    /// Base constructor runs first.
    protected SSLGame() : this("SSLGame")
    {
    }

    /// <summary>
    /// Constructor for SSLGame. Runs before any inheritors.
    /// </summary>
    /// <param name="title">Title of the game window.</param>
    /// <param name="contents">Additional content managers belonging to attached libraries.</param>
    protected SSLGame(string title, params ContentManager[] contents)
    {
        Window.Title = title;
        Content.RootDirectory = "Content";
        Window.AllowUserResizing = true;
        Window.ClientSizeChanged += HandleClientSizeChanged;
        _graphicsManager = HandleGraphicsDesignManager(new GraphicsDeviceManager(this));
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

        // Load settings, and based on game paths, create directories ordered by load order.
        GameSettings settings = LoadSettings();
        // TODO: Make settings fields adjustable so various other projects can have more / less settings than others.
        Directories = GetGameDirectories(settings.GamePaths);
        Directories.Sort();

        // Init w. language from settings.
        Loc.InitalizeLocalizationCulture(settings.Language);

        // WIP: Begin loading directories.
        //  == Textures & Materials
        //  == Prototypes (check ECS I guess?)
        //  == Localization (easy-peasy)
        //  TEMP: Make a breakpoint & double-check that load order is operational. Higher order = higher priority!

        // Assuming there are defined directories to begin with...
        Log($"Loading {Directories.Count} game directories.");
        foreach (GameDirectory directory in Directories)
        {
            // Localization
            if (directory.LocalizationFolder != null)
                Loc.Load(directory.LocalizationFolder);

            // Textures
            if (directory.TexturesFolder != null)
                TextureLoader.Load(directory.TexturesFolder);

            // WIP: Feed configs in from above abstract layer. Load directory willy-nilly and search for a file?
            //  By jove i've GOT IT! Name folders .m, .s, 

            directory.LoadPrototypes();
            
            Log($"...loaded: {directory}");
        }

        // If there aren't any directories, it either is a failure on behalf of the loader, or that one isn't defined.
        // In such case, the entire game folder is a game directory.


        // WIP: DO NOT FORGET CONTENT LOADING!

        // Display ECS status. This called after inheritors.
        Log($"ECS status: {(UseECS ? "on" : "off")}");
        if (UseECS)
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

        Log("SSLGame Root Initialized. Proceeding...");
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
            /*  Once a singular game-path is added to the list, any root-level directory is rendered completely
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

    /// Get game settings from file.
    private static GameSettings LoadSettings()
    {
        var settingsPath = GameSettings.SettingsFilePath;
        var settings = new GameSettings();
        if (!File.Exists(settingsPath))
        {
            GameSettings.ForceCreateDefault(settings);
        }
        else
        {
            IDeserializer deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            try
            {
                var text = File.ReadAllText(settingsPath);
                settings = deserializer.Deserialize<GameSettings>(text);
            }
            catch
            {
                settings = null;
            }

            if (settings is not null) return settings;

            settings = new GameSettings();
            GameSettings.ForceCreateDefault(settings);
        }

        return settings;
    }

    private GumProjectSave? InitializeGum()
    {
        if (string.IsNullOrEmpty(GumFile))
            Log($"No gum project file in Content/Gum in {Title}, {nameof(SSLGame)} Class.", LOG.SYSTEM_WARNING);
        else
            GumFile = Path.Combine("Gum", GumFile);

        // Initialize Gum UI Handling (Some projects may choose not to utilize Gum)
        GumProjectSave? gumProjectSave = null;
        if (string.IsNullOrEmpty(GumFile) || GumFile.Contains("CHANGE_ME")) return gumProjectSave;
        Gum = GumService.Default;
        gumProjectSave = Gum.Initialize(this, GumFile);

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
        GumProjectSave? gumSave = InitializeGum();

        SceneManager = new SceneManager(this, _graphicsManager, _spriteBatch, gumSave);
        Components.Add(SceneManager);

        if (UseECS)
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
        if (UseECS)
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
        if (UseECS)
        {
            SystemManager.Update(gameTime);
        }

        Gum?.Update(gameTime); // Update Gum UI after game update.
    }
}