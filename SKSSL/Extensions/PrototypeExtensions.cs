using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using SKSSL.ECS;
using SKSSL.YAML;

namespace SKSSL.Extensions;

public static class PrototypeExtensions
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Applies additive inheritance from a base prototype into a targe prototypet. Uses Reflection.
    /// Child values take precedence (override), base only fills missing fields.
    /// Works with both Prototype and Entity (including YamlComponents).
    /// </summary>
    public static void ApplyInheritanceOf(this Prototype target, Prototype? baseProto)
    {
        if (baseProto == null) return;
        var chain = new Stack<Type>();
        for (Type? t = baseProto.GetType(); t != null; t = t.BaseType) chain.Push(t);

        foreach (Type type in chain)
        {
            var props = type.GetProperties(Flags);
            foreach (PropertyInfo prop in props)
            {
                if (!prop.CanRead || !prop.CanWrite)
                    continue;

                if (prop.Name is "Type" or "Parent")
                    continue;

                var baseValue = prop.GetValue(baseProto);
                var childValue = prop.GetValue(target);

                if (IsUnset(childValue) && !IsUnset(baseValue)) prop.SetValue(target, baseValue);
            }
        }

        // Additional handling for entities.
        if (baseProto is not Entity baseEntity ||
            target is not Entity targetEntity ||
            !(baseEntity.YamlComponents?.Count > 0)) return;
        targetEntity.YamlComponents ??= [];

        for (int i = 0; i < baseEntity.YamlComponents.Count; i++)
        {
            YamlComponent baseComp = baseEntity.YamlComponents[i];

            bool found = false;

            foreach (YamlComponent targetComp in targetEntity.YamlComponents)
            {
                if (!string.Equals(targetComp.Type, baseComp.Type, StringComparison.OrdinalIgnoreCase))
                    continue;
                found = true;
                MergeYamlComponent(targetComp, baseComp);
                break;
            }

            if (!found)
                targetEntity.YamlComponents.Add(CloneYamlComponent(baseComp));
        }
    }

    private static void MergeYamlComponent(YamlComponent target, YamlComponent baseComp)
    {
        foreach (var kv in baseComp.Entries)
            if (!target.Entries.ContainsKey(kv.Key))
                target.Entries[kv.Key] = kv.Value;
    }

    [Pure]
    private static bool IsUnset(object? value)
    {
        switch (value)
        {
            case null:
                return true;
            case string s:
                return string.IsNullOrEmpty(s);
            case ICollection c:
                return c.Count == 0;
        }

        Type type = value.GetType();
        return type.IsValueType && Equals(value, Activator.CreateInstance(type));
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