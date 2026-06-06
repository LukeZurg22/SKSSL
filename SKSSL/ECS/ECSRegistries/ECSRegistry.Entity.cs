using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using SKSSL.Extensions;

// ReSharper disable UnusedType.Global

namespace SKSSL.ECS;

public sealed class ECSRegistry_Entity : ECSRegistry<Entity>
{
    /// Individual definitions belonging to all prototype instances loaded from yaml.
    /// The inherited Entries dictionary is the "Raw" list.
    /// Handle Key, Entity (ID = 0) Value.
    /// Note that this does NOT indicate active entities in a world! Only registered definitions from file loading!
    public readonly Dictionary<string, Entity> ResolvedGameEntities = [];

    public override void Register(string handle, Entity obj)
    {
        // Register "raw" prototype.
        base.Register(handle, obj);

        // If a provided object's handle is already contained, then it is reasonable to assume that the existing one-
        //  -WILL be overwritten!
        /*if (Contains(handle))
            Log($"Registry already contains {handle} handle, which is being overwritten!");*/

        // Recursive resolution via raw entities.
        foreach (var entityType in RegistryEntries.Keys)
            GetResolvedPrototype(entityType);
    }

    /// Only count the resolved game entities. Raw ones will not suffice.
    public override int Count() => ResolvedGameEntities.Count;

    /// <summary>
    /// Retrieve resolved entities instead of raw.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="definition"></param>
    /// <returns></returns>
    public override bool TryGet(string handle, [MaybeNullWhen(false)] out Entity definition)
        => ResolvedGameEntities.TryGetValue(handle, out definition);

    private Entity? GetResolvedPrototype(string handle)
    {
        // Must be in raw prototypes first.
        if (!RegistryEntries.TryGetValue(handle, out Entity? raw))
            return null;

        // If theres a handle to an already-resolved entity, then return that.
        if (ResolvedGameEntities.TryGetValue(handle, out Entity? resolved))
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

        ResolvedGameEntities[handle] = resolved;
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
        if (current.Inherit != null) foreach (var inherit in current.Inherit)
        {
            Entity? baseProto = GetResolvedPrototype(inherit);
            if (baseProto != null)
                result.ApplyInheritanceOf(baseProto);
        }
        //@formatter:on

        visited.Remove(current.Type);
    }
}