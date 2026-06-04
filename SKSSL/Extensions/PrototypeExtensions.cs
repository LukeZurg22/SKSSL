using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using SKSSL.ECS;
using SKSSL.YAML;

namespace SKSSL.Extensions;

public static class PrototypeExtensions
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    /// <summary>
    /// Applies additive inheritance from a base Prototype/Entity into a target.
    /// Uses Reflection.
    /// Child values take precedence (override), base only fills missing fields.
    /// Works with both Prototype and Entity (including YamlComponents).
    /// </summary>
    public static void ApplyInheritanceOf(this Prototype target, Prototype? baseProto)
    {
        if (baseProto == null) return;

        var props = baseProto.GetType().GetProperties(Flags);

        foreach (PropertyInfo prop in props)
        {
            if (!prop.CanRead || !prop.CanWrite) continue;
            if (prop.Name is "Type" or "Parent") continue; // handled specially

            var childValue = prop.GetValue(target);
            var baseValue = prop.GetValue(baseProto);

            // Additive only: copy only if child doesn't have a meaningful value
            if (IsDefaultOrEmpty(childValue) && !IsDefaultOrEmpty(baseValue))
            {
                prop.SetValue(target, baseValue);
            }
        }

        // Special handling for YamlComponents (additive)
        if (baseProto is not Entity baseEntity ||
            target is not Entity targetEntity ||
            !(baseEntity.YamlComponents?.Count > 0))
            return;

        targetEntity.YamlComponents ??= [];

        var yamlComponents = new HashSet<string>(targetEntity.YamlComponents.Select(c => c.Type),
            StringComparer.OrdinalIgnoreCase);

        foreach (YamlComponent comp in baseEntity.YamlComponents)
        {
            if (!yamlComponents.Contains(comp.Type))
                targetEntity.YamlComponents.Add(CloneYamlComponent(comp));
            
            // WIP: If it already contains existing components, inheritance should also override the fields of existing ones.
        }
    }

    private static bool IsDefaultOrEmpty(object? value)
    {
        return value switch
        {
            null => true,
            string s => string.IsNullOrEmpty(s),
            ICollection c => c.Count == 0,
            _ => false
        };
    }

    /// Helper for cloning YamlComponents.
    [Pure]
    private static YamlComponent CloneYamlComponent(YamlComponent source)
    {
        return new YamlComponent
        {
            Type = source.Type, Entries = source.Entries.Count != 0
                ? new Dictionary<string, object?>(source.Entries)
                : new Dictionary<string, object?>()
        };
    }
}