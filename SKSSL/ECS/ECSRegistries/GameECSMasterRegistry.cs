using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

// ReSharper disable UnusedMember.Global

namespace SKSSL.ECS;

/// <summary>
/// Storing all prototype definitions.
/// </summary>
public abstract class GameECSMasterRegistry
{
    /*
     * Here's The Pattern:
     * Source Generators assign Type Handles --> Types.
     * Static constructor creates Types --> Registries.
     * File loading stores and loads in the order of Type -> Registry -> Operations.
     */

    #region TypeDef (One-And-Done w. SourceGen Calls!)

    /// Raw class type-definitions in development environment -only-. These are used in inheritance rules and are
    /// project-specific. Used for deserialization, and is filled-out by the Source Generator.
    [UsedImplicitly] public static readonly Dictionary<string, Type> TypeDefinitions = new();

    public static IReadOnlyList<Type> RegisteredGameProtoTypes => TypeDefinitions.Values.ToList().AsReadOnly();

    /// Called directly from Source Generator.
    [UsedImplicitly]
    public static void RegisterTypeDefinition(string handle, Type type) => TypeDefinitions[handle] = type;

    [Pure]
    public static bool TryGetRegisteredTypeDefinition(string typeName, out Type type)
        => TypeDefinitions.TryGetValue(typeName, out type!);

    /// Handles / IDs MUST be unique per-prototype, else there may be some conflicts!
    private static readonly Dictionary<string, Type> HandleToType = new();

    #endregion

    #region Registry Onboarding

    /// Every custom entity / prototype type may have a custom registry that handles how it is loaded!
    /// This particular property is assigned directly from Source Generator.
    [UsedImplicitly]
    public static Dictionary<Type, ECSRegistry> Registries { get; } = new();

    static GameECSMasterRegistry()
    {
        // Scan once via reflection for custom registry types.
        foreach (Type type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(a => a.GetTypes()))
        {
            Type? baseType = type.BaseType;

            if (baseType?.IsGenericType != true)
                continue;

            if (baseType.GetGenericTypeDefinition() != typeof(ECSRegistry<>))
                continue;

            Type targetType = baseType.GetGenericArguments()[0];

            Registries[targetType] =
                (ECSRegistry)Activator.CreateInstance(type)!;
        }
    }

    /// <summary>
    /// Register a prototype entry dynamically to a register based on its type, defaulting to the generic prototypes
    /// registry.
    /// </summary>
    /// <remarks>
    /// Automatically registers definitions as a "source:handle" arrangement.
    /// No additional checks are made here, as they are made up the call chain.
    /// </remarks>
    internal static void RegisterLoadedPrototype(Type type, Prototype definition)
    {
        // For O(1) retrieval.
        HandleToType[definition.Handle] = type;
        GetRegistry(type).Register(definition.Handle, definition);
    }

    #endregion

    #region Utility Methods

    /// Clears all definitions in registry. Called by Source Generator.
    // ReSharper disable once UnusedMember.Global
    public static void Clear()
    {
        TypeDefinitions.Clear();
        foreach (ECSRegistry registry in Registries.Values) registry.Clear();
    }

    /// <returns>Sum total of all registry entry values.</returns>
    // ReSharper disable once UnusedMember.Global
    public static int Count() => Registries.Values.Sum(registry => registry.Count());

    [Pure]
    private static ECSRegistry GetRegistry(Type targetType)
    {
        return Registries.TryGetValue(targetType, out ECSRegistry? registry)
            ? registry
            : ECSRegistry_RawPrototype.Instance; // Default to basic prototypes. What can I say?
    }

    #endregion

    #region TryGet

    /// Generically-typed alternative to the other TryGetPrototype for when you -know- what you want at compile time.
    public static bool TryGetPrototype<T>(string handle, out T prototype) where T : class, new()
    {
        // Check that the handle has a corresponding type definition.
        prototype = default!;
        if (!HandleToType.TryGetValue(handle, out Type? type))
            return false;

        // Get registry associated with the acquired type, and get prototype directly without a cast.
        ECSRegistry ecsRegistry = GetRegistry(type);
        return ((ECSRegistry<T>)ecsRegistry).TryGet(handle, out prototype!);
    }

    public static bool TryGetPrototype(string handle, out Prototype prototype)
    {
        // Check that the handle has a corresponding type definition.
        prototype = default!;
        if (!HandleToType.TryGetValue(handle, out Type? type))
            return false;

        // Get registry associated with the acquired type.
        ECSRegistry ecsRegistry = GetRegistry(type);
        if (!ecsRegistry.TryGet(handle, out var thing))
            return false;

        // Return an acquired entry from the registry and cast.
        prototype = (thing as Prototype)!;
        return true;
    }

    #endregion
}