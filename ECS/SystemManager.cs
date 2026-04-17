using System.Reflection;
using Microsoft.Xna.Framework;
using SKSSL.Scenes;
using static SKSSL.DustLogger;

// ReSharper disable SuspiciousTypeConversion.Global

// ReSharper disable PossibleMultipleEnumeration

// ReSharper disable RedundantAttributeUsageProperty

namespace SKSSL.ECS;

/// <summary>
/// Manages all system draw and update calls. Should be added once per Game instance.
/// </summary>
public class SystemManager
{
    private readonly List<EntitySystem> _systems = [];
    private readonly List<int> _updateSystemIndices = [];
    private readonly List<int> _drawSystemIndices = [];
    private GraphicsDeviceManager _graphics = null!;

    /// <summary>
    /// Registers all systems within a provided world.
    /// </summary>
    /// <param name="graphics"></param>
    /// <exception cref="InvalidOperationException">A defined system type doesn't have a valid World constructor.</exception>
    /// <remarks>Called by <see cref="BaseWorld"/>.Initialize()</remarks>
    public void RegisterAll(GraphicsDeviceManager graphics)
    {
        _graphics = graphics;
        _systems.Clear();
        // Get all loaded assemblies.
        Log("...reading system assemblies...");
        var systemTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetCustomAttributes(typeof(RegisterSystemAttribute), false).Length > 0)
            .OrderBy(t =>
            {
                var attr = (RegisterSystemAttribute)t.GetCustomAttributes(typeof(RegisterSystemAttribute), false)[0];
                return attr.Order;
            }).ToList();

        // Read all registered types.
        Log($"...loading systems from {systemTypes.Count} types...");
        foreach (Type type in systemTypes)
        {
            // Systems no longer have WORLD constructor due to accessed global context.
            // OLD: All systems have (World) constructor
            //ConstructorInfo constructor = type.GetConstructor([typeof(BaseWorld)])
            //                              ?? throw new InvalidOperationException(
            //                                  $"System {type.Name} missing (World world) constructor");

            ConstructorInfo constructor = type.GetConstructor([])
                                          ?? throw new InvalidOperationException(
                                              $"No blank constructor for system type {type.Name}");

            //var system = constructor.Invoke([world]);
            var system = constructor.Invoke([]);

            // Add system to System Manager.
            Add((system as EntitySystem)!);
        }

        Log($"Completed registration of {_systems.Count} Systems.");
    }

    /// For manual registration.
    public void Add(EntitySystem system)
    {
        if (system is IUpdateSystem)
            _updateSystemIndices.Add(_updateSystemIndices.Count);
        if (system is IDrawSystem)
            _drawSystemIndices.Add(_updateSystemIndices.Count);
        _systems.Add(system);
        system.Initialize();
    }

    /// <summary>
    /// Loops through system update indices and Updates corresponding systems in the systems list.
    /// </summary>
    /// <param name="gameTime">By-reference gameTime object for system update.</param>
    public void Update(GameTime gameTime)
    {
        foreach (int index in _updateSystemIndices) (_systems[index] as IUpdateSystem)?.Update(gameTime);
    }

    /// <summary>
    /// Loops through system Draw indices and Draws corresponding systems in the systems list.
    /// </summary>
    /// <param name="gameTime">By-reference gameTime object for system Draw.</param>
    public void Draw(GameTime gameTime)
    {
        foreach (var index in _drawSystemIndices) (_systems[index] as IDrawSystem)?.Draw(gameTime);
    }
}