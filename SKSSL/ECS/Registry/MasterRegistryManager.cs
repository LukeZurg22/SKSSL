using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace SKSSL.ECS.Registry;

/// <summary>
/// Storing all prototype definitions.
/// </summary>
public abstract class MasterRegistryManager
{
    /*
     * Here's The Pattern:
     * Source Generators assign Type Handles --> Types.
     * Static constructor creates Types --> Registries.
     * File loading stores and loads in the order of Type -> Registry -> Operations.
     */

    #region TypeDef & Registries (One-And-Done w. SourceGen Calls!)

    /// Raw class type-definitions in development environment -only-. These are used in inheritance rules and are
    /// project-specific. Used for deserialization, and is filled-out by the Source Generator.
    [UsedImplicitly] public static readonly Dictionary<string, Type> TypeDefinitions = new();

    /// Every custom entity / prototype type may have a custom registry that handles how it is loaded!
    /// This particular property is assigned directly from Source Generator.
    [UsedImplicitly]
    public static Dictionary<Type, Registry> Registries { get; } = new();

    public static IReadOnlyList<Type> RegisteredGameRegistryTypes => TypeDefinitions.Values.ToList().AsReadOnly();

    /// Registers a sanitized type handle to a definitions dictionary, and populates the Type to Registry
    /// dictionary. /// Called directly from Source Generator.
    [UsedImplicitly]
    public static void RegisterTypeDefinition(string handle, Type type)
    {
        TypeDefinitions[handle] = type;

        Type? rootType = type;
        while (rootType != null)
        {
            Type? registryType = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t =>
                    t.BaseType?.IsGenericType == true &&
                    t.BaseType.GetGenericTypeDefinition() == typeof(Registry<>) &&
                    t.BaseType.GetGenericArguments()[0] == rootType);

            if (registryType != null)
            {
                Registries[type] = (Registry)Activator.CreateInstance(registryType)!;
                return;
            }

            rootType = rootType.BaseType;
        }

        // Default to raw prototype.
        Registries[type] = RawPrototypeRegistry.Instance;
    }

    /// <summary>
    /// Get expected runtime Type.
    /// </summary>
    /// <param name="typeName">Sanitized handle of Type class.</param>
    /// <param name="type">Runtime Type of handle.</param>
    /// <returns>True if found; false if not!</returns>
    [Pure]
    public static bool TryGetRegisteredTypeDefinition(string typeName, out Type type)
        => TypeDefinitions.TryGetValue(typeName, out type!);

    #endregion

    #region Prototype Onboarding

    /// Unique per-prototype Handles-Types dictionary to avoid conflicts.
    private static readonly Dictionary<string, Type> PrototypeHandleToType = new();

    /// <summary>
    /// Register a prototype entry dynamically to a register based on its type, defaulting to the generic prototypes
    /// registry. Do NOT call this manually unless you know what you are doing!
    /// </summary>
    /// <remarks>
    /// Automatically registers definitions as a "source:handle" arrangement.
    /// No additional checks are made here, as they are made up the call chain.
    /// </remarks>
    public static bool TryRegisterPrototype(string typeName, Prototype prototype)
    {
        if (TypeDefinitions.TryGetValue(typeName, out Type? type))
        {
            return RegisterInto(type, prototype);
        }

        Type? current = prototype.GetType();
        while (current != null)
        {
            // Prefer an exact match on the short name if it exists
            string candidateName = current.Name;
            if (candidateName.EndsWith("Prototype", StringComparison.OrdinalIgnoreCase))
                candidateName = candidateName[..^9];

            if (TypeDefinitions.TryGetValue(candidateName, out type) ||
                TypeDefinitions.TryGetValue(current.Name, out type) ||
                Registries.ContainsKey(current))
            {
                // Remember the original typeName so future lookups are fast
                return RegisterInto(type ?? current, prototype);
            }

            // Stop when we reach the root
            if (current == typeof(Prototype))
                break;

            current = current.BaseType;
        }

        if (current == null)
            Log(new InvalidCastException($"\'{typeName}\' type provided for prototype \'{prototype.Handle}\', " +
                                         $"which is not a valid type definition in the registry."), LOG.FILE_ERROR);
        return true;
    }

    private static bool RegisterInto(Type type, Prototype prototype)
    {
        if (!Registries.TryGetValue(type, out Registry? registry))
        {
            // Should never happen for Prototype, but be defensive
            return false;
        }

        switch (prototype.Replace)
        {
            // For O(1) retrieval. Register handle with unique ref.
            case null:
                PrototypeHandleToType[prototype.GetFullHandle()] = type;
                break;
            // If replace is assigned, then attempt to replace the original prototype handle.
            default:
            {
                var replaceHandle = prototype.GetFullHandle(prototype.Replace);
                if (PrototypeHandleToType.ContainsKey(replaceHandle))
                    PrototypeHandleToType[replaceHandle] = type;
                else
                    // If the original handle that is being attempted to be replaced doesn't exist... uh oh!
                    Log($"{prototype.GetFullHandle()} attempted to replace {replaceHandle} which does not exist.",
                        LOG.FILE_ERROR);
                break;
            }
        }

        registry.Register(prototype.Handle, prototype);
        return true;
    }

    #endregion

    #region Utility Methods

    /// Clears all definitions in registry. Called by Source Generator.
    // ReSharper disable once UnusedMember.Global
    public static void Clear()
    {
        TypeDefinitions.Clear();
        foreach (Registry registry in Registries.Values) registry.Clear();
    }

    /// <returns>Sum total of all registry entry values.</returns>
    // ReSharper disable once UnusedMember.Global
    public static int Count() => Registries.Values.Sum(registry => registry.Count());

    /// <summary>
    /// Retrieve a dedicated registry in the system.
    /// </summary>
    /// <param name="targetType"></param>
    /// <returns></returns>
    [Pure]
    public static Registry GetRegistry(Type targetType)
    {
        Type? current = targetType;

        // Get registry, or continue up inheritance chain until one is found; defaulting to Prototypes
        //  if all else fails. This means the optimal scenario is when a custom registry type is created for every
        //  unique type. Seems fair!
        while (current != null)
        {
            if (Registries.TryGetValue(current, out Registry? registry))
                return registry;

            current = current.BaseType;
        }

        return RawPrototypeRegistry.Instance;
    }

    /// <summary>
    /// Hyper-specific overload for GetRegistry for when you know precisely what registry class derivative, and what
    /// registry-handled type is being used. This could crash. 
    /// </summary>
    /// <exception cref="InvalidCastException">
    /// Thrown if expected type T does not have a registry.
    /// This usually is the fault of the user, or the Source Generators.
    /// </exception>
    public static TRegistry GetRegistry<T, TRegistry>()
        where T : class, new()
        where TRegistry : Registry<T>
        => (TRegistry)Registries[typeof(T)];

    #endregion

    #region TryGet

    /// Generically-typed alternative to the other TryGetPrototype for when you -know- what you want at compile time.
    public static bool TryGetPrototype<T>(string handle, out T prototype) where T : class, new()
    {
        // Check that the handle has a corresponding type definition.
        prototype = default!;
        if (!PrototypeHandleToType.TryGetValue(handle, out Type? type))
            return false;

        // Get registry associated with the acquired type, and get prototype directly without a cast.
        var ecsRegistry = (Registry<T>)GetRegistry(type);
        return ecsRegistry.TryGet(handle, out prototype!);
    }

    public static bool TryGetPrototype(string handle, out Prototype prototype)
    {
        // Check that the handle has a corresponding type definition.
        prototype = default!;
        if (!PrototypeHandleToType.TryGetValue(handle, out Type? type))
            return false;

        // Get registry associated with the acquired type.
        Registry registry = GetRegistry(type);
        if (!registry.TryGet(handle, out var thing))
            return false;

        // Return an acquired entry from the registry and cast.
        prototype = (thing as Prototype)!;
        return true;
    }

    #endregion
}