using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SKSSL.Extensions;

// ReSharper disable UnusedType.Global

namespace SKSSL.ECS.Registry;

/// <remarks>
/// Use this Registry type, and any inheriting kinds only if the ECS is enabled. There may be unexpected behaviour,
/// otherwise. Used for entity Definitions, NOT active entities.
/// </remarks>
public sealed class EntityRegistry : Registry<Entity>
{
    /// Individual definitions belonging to all prototype instances loaded from yaml.
    /// The inherited Entries dictionary is the "Raw" list.
    /// Handle Key, Entity (ID = 0) Value.
    /// Note that this does NOT indicate active entities in a world! Only registered definitions from file loading!
    public readonly Dictionary<string, Entity> ResolvedGameEntityDefinitions = [];

    public override object? Register(string handle, Entity obj)
    {
        // Register prototype as part of RegistryEntries "raw" list, rather than make a new list.
        base.Register(handle, obj);

        // If a provided object's handle is already contained, then it is reasonable to assume that the existing one-
        //  -WILL be overwritten!
        /*if (Contains(handle))
            Log($"Registry already contains {handle} handle, which is being overwritten!");*/

        // Recursive resolution via raw entities.
        foreach (var entityType in RegistryEntries.Keys)
            GetResolvedPrototype(entityType);

        return null;
    }

    /// Only count the resolved game entities. Raw ones will not suffice.
    public override int Count() => ResolvedGameEntityDefinitions.Count;

    /// <summary>
    /// Retrieve resolved entities instead of raw.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="definition"></param>
    /// <returns></returns>
    public override bool TryGet(string handle, [MaybeNullWhen(false)] out Entity definition)
        => ResolvedGameEntityDefinitions.TryGetValue(handle, out definition);

    private Entity? GetResolvedPrototype(string handle)
    {
        // Must be in raw prototypes first.
        if (!RegistryEntries.TryGetValue(handle, out Entity? raw))
            return null;

        // If theres a handle to an already-resolved entity, then return that.
        if (ResolvedGameEntityDefinitions.TryGetValue(handle, out Entity? resolved))
            return resolved;

        resolved = new Entity
        {
            Source = raw.Source,
            Type = raw.Type,
            Handle = raw.Handle,
        };

        resolved.ApplyInheritanceOf(raw);

        var visited = new HashSet<string>(); // Cycle detection
        ResolveInheritanceRecursive(raw, resolved, visited);

        ResolvedGameEntityDefinitions[handle] = resolved;
        return resolved;
    }

    private void ResolveInheritanceRecursive(Entity current, Entity result, HashSet<string> visited)
    {
        if (!visited.Add(current.Type))
        {
            Log($"Inheritance cycle detected involving '{current.Type}'", LOG.FILE_ERROR);
            return;
        }

        //@formatter:off
        if (current.Inherit.Length > 0)
        foreach (var inherit in current.Inherit)
        {
            Entity? baseProto = GetResolvedPrototype(inherit);
            if (baseProto != null)
                result.ApplyInheritanceOf(baseProto);
        }
        //@formatter:on

        visited.Remove(current.Type);
    }
}