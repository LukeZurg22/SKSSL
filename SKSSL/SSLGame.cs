using System;
using System.Collections.Generic;
using System.IO;
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
using SKSSL.Localization;
using SKSSL.Scenes;
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
    #region Fields

    /// Title of game.
    public string Title => Window.Title;

    /// Total time played for this game session.
    public DateTime GameplayTime;

    /// Ultimate toggle to use ECS service. Enable this at project initialization.
    /// To use, add the following to the game class inheriting SSLGame:
    /// <code>
    /// static MyGameClass() => UseECS = true;
    /// </code>
    public static bool UseECS = false;

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

    /// <summary>
    /// The Project Gum UI file that will dictate how UI is loaded.
    /// <code>
    /// Example: "Gum/SolKom.gumx"
    /// </code>
    /// </summary>
    public static string GumFile = "CHANGE_ME";

    /// All content directories contained in the game folder. (E.g. game, mods ➡ etc.)
    public readonly List<GameDirectory> GameContentDirectories = [];

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

    protected SSLGame() : this("SSLGame")
    {
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
        MouseHandler = new MouseWrapper(_graphicsManager);
        StyleSheet.LoadStyles();
        if (string.IsNullOrEmpty(gumFile))
        {
            Log($"No gum project file in Content/Gum in {title}, {nameof(SSLGame)}", LOG.SYSTEM_WARNING);
        }
        else
        {
            GumFile = Path.Combine("Gum", gumFile);
        }

        var services = new ServiceCollection();
        LoadServices(services);
        GameServices = services.BuildServiceProvider();

        // Assign static-access content managers.
        ContentManagers.Add(Content);
        ContentManagers.AddRange(contents);

        // WIP: Load settings, and based on game paths, load content. Game content is no different from the rest!
        GameSettings settings = LoadSettings();
        // If there are designated game paths, create Game Directories.
        if (settings.GamePaths.Count > 0)
        {
            foreach (var gamePath in settings.GamePaths)
            {
                var directory = new GameDirectory(gamePath.Path, gamePath.Order);
                GameContentDirectories.Add(directory);
            }
        }
        // No designated game paths means that a specialized dynamic one will be needed. Mods basically don't
        //  exist in this arrangement.
        else
        {
                GameContentDirectories.Add(new GameDirectory());
            
        }

        // Initialize all static paths, which the developer must have defined!
        // Includes load-order implementation. Higher values override lower values.
        // TODO: Add a way to change load order priorities in game directories. Likely requires a file? Master file?
        //  A file per-game folder means version mismatches per file change that breaks every update.
        //  Ergo, a master file may be the best solution.
        // WIP: CONTENT LOADING!
        //var gameDirectories = StaticGameLoader.GetAllGameDirectories();
        //GameContentDirectories = gameDirectories.OrderBy(d => d.LoadOrder).ToList();

        // Display ECS status. This constructor is called after inheritors.
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
        // Initialize Gum UI Handling (Some projects may choose not to utilize Gum)
        GumProjectSave? gumSave = null;
        if (!string.IsNullOrEmpty(GumFile))
        {
            Gum = GumService.Default;
            gumSave = Gum.Initialize(this, GumFile);
        }

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