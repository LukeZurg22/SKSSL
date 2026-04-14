using System.Reflection;
using Microsoft.Xna.Framework;
using SKSSL.Scenes;
using static SKSSL.DustLogger;

// ReSharper disable PossibleMultipleEnumeration

// ReSharper disable RedundantAttributeUsageProperty

namespace SKSSL.ECS;

/// <summary>
/// Manages all system draw and update calls. Should be added once per Game instance.
/// </summary>
public class SystemManager
{
    private readonly List<IUpdateSystem> _updateSystems = [];
    private readonly List<IDrawSystem> _drawSystems = [];

    /// <summary>
    /// Registers all systems within a provided world.
    /// </summary>
    /// <exception cref="InvalidOperationException">A defined system type doesn't have a valid World constructor.</exception>
    /// <remarks>Called by <see cref="BaseWorld"/>.Initialize()</remarks>
    public void RegisterAll()
    {
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

            switch (system)
            {
                case IUpdateSystem update:
                    _updateSystems.Add(update);
                    break;
                case IDrawSystem draw:
                    _drawSystems.Add(draw);
                    break;
            }
        }

        Log($"Completed registration of {_updateSystems.Count + _drawSystems.Count} Systems.");
    }

    /// For manual registration.
    public void Add(IUpdateSystem system) => _updateSystems.Add(system);

    /// For manual registration.
    public void Add(IDrawSystem system) => _drawSystems.Add(system);

    public void Update(GameTime dt)
    {
        foreach (IUpdateSystem system in _updateSystems)
            system.Update(dt);
    }

    public void Draw(GameTime gameTime)
    {
        foreach (IDrawSystem system in _drawSystems)
            system.Draw(gameTime);
    }
}

// Interfaces
public interface IUpdateSystem
{
    void Update(GameTime dt);
}

public interface IDrawSystem
{
    void Draw(GameTime gameTime);
}

/// <summary>
/// Marks the class this is attribute is tied to as viable for the automatic registry system.
/// World data is provided on-registration.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RegisterSystemAttribute : Attribute
{
    // To control order or phase
    public int Order { get; set; } = 0;
}