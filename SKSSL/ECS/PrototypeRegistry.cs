using System;
using System.Collections.Generic;
using SKSSL.Extensions;
using static SKSSL.DustLogger;

namespace SKSSL.ECS;

/// <summary>
/// Storing all prototype definitions.
/// </summary>
public abstract class PrototypeRegistry
{
    #region System Type Definitions

    /// Raw class type-definitions in development environment -only-. These are used in inheritance rules and are
    /// project-specific.
    // ReSharper disable once CollectionNeverUpdated.Global
    public static readonly Dictionary<string, Type> Definitions = new();

    /// Outputs a string handle based on provided type linked to class-type definition.
    // ReSharper disable once UnusedMember.Global
    public static bool TryGetTypeHandle(Type type, out string typeHandle)
    {
        typeHandle = "";
        foreach (var definition in Definitions)
        {
            if (definition.Value != type)
                continue;
            typeHandle = definition.Key;
            return true;
        }

        return false;
    }

    /// Clears all definitions in registry. Called by Source Generator.
    // ReSharper disable once UnusedMember.Global
    public static void Clear()
    {
        Definitions.Clear();
        RawPrototypes.Clear();
    }

    #endregion

    public static bool TypeDefined(string name) => Definitions.Count != 0 && Definitions.ContainsKey(name);

    // ReSharper disable once UnusedMember.Global
    public static bool TypeDefined(Type type) => Definitions.Count != 0 && Definitions.ContainsValue(type);

    /// Individual definitions belonging to all prototype instances loaded from yaml.
    /// Handle Key, Entity (ID = 0) Value
    public static readonly Dictionary<string, Prototype> ResolvedGamePrototypes = [];

    /// Individual definitions belonging to all prototype instances loaded from yaml.
    public static readonly Dictionary<string, Prototype> RawPrototypes = [];

    /// <summary>
    /// Inquiry to the entity manager for a possible entity definition.
    /// </summary>
    /// <param name="handle">Full Source:Handle ID that the Entity Registry definitions should possess.</param>
    /// <returns>True if a template was found. False if one was not.</returns>
    public static bool ContainsPrototype(string handle) => RawPrototypes.ContainsKey(handle);

    public static void Insert(Prototype newPrototype)
    {
        if (!SSLGame.UseECS)
        {
            Log($"Insertion of prototype {newPrototype.GetUniqueInternalRef()} into registry failed! ECS is disabled!",
                LOG.SYSTEM_WARNING);
            return;
        }

        if (RawPrototypes.ContainsKey(newPrototype.Handle))
            Log(
                $"Raw game prototype storage already contains {newPrototype.Handle} handle, which is being overwritten!");

        // Allow override of existing, but a warning was definitely needed.
        // WARN: Load order might not be accommodated-for just yet. Prototypes should not 
        RawPrototypes[newPrototype.Handle] = newPrototype;

        // Resolve inheritance.
        foreach (var entityType in Definitions.Keys)
        {
            GetResolvedPrototype(entityType); // triggers recursive resolution
        }
    }

    public static Prototype? GetResolvedPrototype(string handle)
    {
        // Must be in raw prototypes first.
        if (!RawPrototypes.TryGetValue(handle, out Prototype? raw))
            return null;

        // If theres a handle to an already-resolved entity, then return that.
        if (ResolvedGamePrototypes.TryGetValue(handle, out var resolved))
            return resolved;

        resolved = new Prototype
        {
            Source = raw.Source,
            Type = raw.Type,
            Handle = raw.Handle,
        };

        resolved.ApplyInheritanceOf(raw);

        var visited = new HashSet<string>(); // Cycle detection
        ResolveInheritanceRecursive(raw, resolved, visited);

        ResolvedGamePrototypes[handle] = resolved;
        return resolved;
    }

    private static void ResolveInheritanceRecursive(Prototype current, Prototype result, HashSet<string> visited)
    {
        if (!visited.Add(current.Type))
        {
            Log($"Inheritance cycle detected involving '{current.Type}'", LOG.FILE_ERROR);
            return;
        }

        if (!string.IsNullOrEmpty(current.Parent))
        {
            Prototype? baseProto = GetResolvedPrototype(current.Parent);
            if (baseProto != null)
            {
                result.ApplyInheritanceOf(baseProto);
            }
        }

        visited.Remove(current.Type);
    }

    /// <summary>
    /// Register an entity Definition raw or template according to <see cref="Prototype"/>.
    /// </summary>
    /// <remarks>
    /// Automatically registers definitions as a "source:handle" arrangement.
    /// </remarks>
    internal static void RegisterPrototype(Prototype definition)
        => RawPrototypes[definition.GetUniqueInternalRef()] = definition;

    /// <summary>
    /// Safe[r] TryGet method to retrieve an Entity Definition *OR* Template using a reference id.
    /// </summary>
    /// <returns>True if a template was found. False if one was not. The output is also Null if one was not found.</returns>
    public static bool TryGetPrototype<T>(string handle, out T? definition) where T : Prototype
    {
        var gotValue = TypeDefined(handle);
        if (RawPrototypes[handle] is T found)
        {
            definition = found;
            return true;
        }

        definition = null;
        return gotValue;
    }
}