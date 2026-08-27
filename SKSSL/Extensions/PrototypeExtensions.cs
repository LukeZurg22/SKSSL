using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Reflection;
using SKSSL.ECS;

namespace SKSSL.Extensions;

public static class PrototypeExtensions
{
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    /// <summary>
    /// Applies additive inheritance from a base prototype into a target prototype. Uses Reflection.
    /// Child values take precedence (override), base only fills missing fields.
    /// Works with both Prototype and Entity (including YamlComponents).
    /// </summary>
    public static void ApplyInheritanceOf(this Entity target, Entity? baseProto)
    {
        if (baseProto == null) return;

        Type targetType = target.GetType();
        Type baseType = baseProto.GetType();

        // Collect the inheritance chain of the *base* prototype (most-base first)
        var chain = new Stack<Type>();
        for (Type? t = baseType; t != null && t != typeof(object); t = t.BaseType)
            chain.Push(t);

        foreach (Type typeInChain in chain)
        foreach (PropertyInfo declaredProp in typeInChain.GetProperties(Flags))
        {
            if (!declaredProp.CanRead)
                continue;

            // Skip infrastructure properties
            if (declaredProp.Name is nameof(Entity.Type)
                or nameof(Entity.Abstract)
                or nameof(Entity.Inherit))
                continue;

            // Resolve the property on the *actual* runtime types
            PropertyInfo? baseProp = GetProperty(baseType, declaredProp.Name);
            PropertyInfo? targetProp = GetProperty(targetType, declaredProp.Name);

            if (baseProp is null || targetProp is null)
                continue;

            if (!targetProp.CanWrite)
                continue;

            object? baseValue = baseProp.GetValue(baseProto);
            object? childValue = targetProp.GetValue(target);

            if (IsUnset(childValue) && !IsUnset(baseValue))
                targetProp.SetValue(target, baseValue);
        }

        // Additional handling for entities.
        if (!(baseProto.YamlComponents?.Count > 0))
            return;

        target.YamlComponents ??= [];

        for (int i = 0; i < baseProto.YamlComponents.Count; i++)
        {
            ComponentYaml baseComp = baseProto.YamlComponents[i];

            bool found = false;

            foreach (ComponentYaml targetComp in target.YamlComponents)
            {
                if (!string.Equals(targetComp.Type, baseComp.Type, StringComparison.OrdinalIgnoreCase))
                    continue;
                found = true;
                MergeProtoComponent(targetComp, baseComp);
                break;
            }

            if (!found)
                target.YamlComponents.Add(CloneYamlComponent(baseComp));
        }
    }

    private static PropertyInfo? GetProperty(Type type, string name)
    {
        // Prefer the property as it is seen on the concrete type
        return type.GetProperty(name, Flags);
    }

    private static void MergeProtoComponent(ComponentYaml target, ComponentYaml baseComp)
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
    private static ComponentYaml CloneYamlComponent(ComponentYaml source)
    {
        return new ComponentYaml
        {
            Type = source.Type, Entries = source.Entries.Count != 0
                ? new Dictionary<string, object?>(source.Entries)
                : new Dictionary<string, object?>()
        };
    }
}