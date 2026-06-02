using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using SKSSL.Scenes;

// ReSharper disable UnusedMember.Global
// ReSharper disable ConvertIfStatementToSwitchStatement
// ReSharper disable SuspiciousTypeConversion.Global
// ReSharper disable PossibleMultipleEnumeration
// ReSharper disable RedundantAttributeUsageProperty

namespace SKSSL.ECS;

/// <summary>
/// Manages all system draw and update calls. Should be added once per Game instance.
/// </summary>
public abstract class SystemManager
{
    private static readonly List<int> _updateSystemIndices = [];
    private static readonly List<int> _drawSystemIndices = [];

    private static readonly List<EntitySystem> _allSystems = [];

    public static IReadOnlyList<EntitySystem> AllSystems => _allSystems.AsReadOnly();

    /// Overload called by source generator.
    public static void Register<T>() where T : EntitySystem, new() => Register(new T());

    /// Called by source generator.
    public static void Register(EntitySystem system)
    {
        _allSystems.Add(system);

        // Add system to Update and/or Draw flow.
        if (system is IUpdateSystem)
            _updateSystemIndices.Add(_updateSystemIndices.Count);
        if (system is IDrawSystem)
            _drawSystemIndices.Add(_drawSystemIndices.Count);
    }

    public static void Clear() => _allSystems.Clear();

    public static void ForEach(Action<EntitySystem> action)
    {
        foreach (EntitySystem system in _allSystems)
            action(system);
    }

    /// <summary>
    /// Initializes all systems. Assumes they have been registered beforehand
    /// </summary>
    /// <exception cref="InvalidOperationException">A defined system type doesn't have a valid World constructor.</exception>
    /// <remarks>Called by <see cref="BaseWorld"/>.Initialize()</remarks>
    public static void Initialize()
    {
        // Read all registered types.
        Log($"...initializing {AllSystems.Count} systems...");
        foreach (EntitySystem system in AllSystems)
            system.Initialize();

        Log("Completed Systems Init.");
    }

    /// <summary>
    /// Loops through system update indices and Updates corresponding systems in the systems list.
    /// </summary>
    /// <param name="gameTime">By-reference gameTime object for system update.</param>
    public static void Update(GameTime gameTime)
    {
        foreach (int index in _updateSystemIndices)
            (AllSystems[index] as IUpdateSystem)?.Update(gameTime);
    }

    /// <summary>
    /// Loops through system Draw indices and Draws corresponding systems in the systems list.
    /// </summary>
    /// <param name="gameTime">By-reference gameTime object for system Draw.</param>
    public static void Draw(GameTime gameTime)
    {
        foreach (var index in _drawSystemIndices)
            (AllSystems[index] as IDrawSystem)?.Draw(gameTime);
    }
}