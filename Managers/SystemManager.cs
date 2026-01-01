using System.Reflection;
using Microsoft.Xna.Framework;
using SKSSL.Scenes;

namespace SKSSL.Managers;

/// <summary>
/// Manages all system draw and update calls. Should be added once per Game instance.
/// </summary>
public class SystemManager
{
    private readonly List<IUpdateSystem> _updateSystems = [];
    private readonly List<IDrawSystem> _drawSystems = [];
    
    public void RegisterAll(BaseWorld world)
    {
        var systemTypes = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsClass && !t.IsAbstract &&
                        t.GetCustomAttributes(typeof(RegisterSystemAttribute), false).Length > 0)
            .OrderBy(t =>
            {
                var attr = (RegisterSystemAttribute)t.GetCustomAttributes(typeof(RegisterSystemAttribute), false)[0];
                return attr.Order;
            });

        foreach (Type type in systemTypes)
        {
            // All systems have (World) constructor
            ConstructorInfo constructor = type.GetConstructor([typeof(BaseWorld)])
                                          ?? throw new InvalidOperationException(
                                              $"System {type.Name} missing (World world) constructor");

            var system = constructor.Invoke([world]);

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

        DustLogger.Log($"Auto-registered {_updateSystems.Count + _drawSystems.Count} systems",
            DustLogger.LOG.INFORMATIONAL_PRINT);
    }

    public void Register<T>() where T : new()
    {
        var system = new T();

        // Auto-detect interfaces
        if (system is IUpdateSystem update)
            _updateSystems.Add(update);

        if (system is IDrawSystem draw)
            _drawSystems.Add(draw);
    }

    // For manual registration
    public void Add(IUpdateSystem system) => _updateSystems.Add(system);
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