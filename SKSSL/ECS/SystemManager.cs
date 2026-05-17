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

    /// <summary>
    /// Registers all systems within a provided world.
    /// </summary>
    /// <exception cref="InvalidOperationException">A defined system type doesn't have a valid World constructor.</exception>
    /// <remarks>Called by <see cref="BaseWorld"/>.Initialize()</remarks>
    public void RegisterAll()
    {
        // TODO: Switch from reflection to AoT Source Generators for efficiency. See NOTES.txt in personal folder.
        
        _systems.Clear();
        // Get all loaded assemblies.
        Log("...reading system assemblies...");
        var systems = SystemRegistry.AllSystems;

        // Read all registered types.
        Log($"...loading systems from {systems.Count} types...");
        foreach (EntitySystem system in systems)
        {
            // Add system to System Manager.
            Add(system);
        }

        Log($"Completed registration of {_systems.Count} Systems.");
    }

    /// For manual registration.
    public void Add(EntitySystem system)
    {
        switch (system)
        {
            case IUpdateSystem:
                _updateSystemIndices.Add(_updateSystemIndices.Count);
                break;
            case IDrawSystem:
                _drawSystemIndices.Add(_updateSystemIndices.Count);
                break;
        }

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