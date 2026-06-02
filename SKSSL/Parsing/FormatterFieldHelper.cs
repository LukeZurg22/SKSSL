using System;
using System.Collections.Generic;
using System.Reflection;
using YamlDotNet.Serialization;


namespace SKSSL;

public static class FormatterFieldHelper
{
    private const BindingFlags Flags =
        BindingFlags.Public |
        BindingFlags.NonPublic |
        BindingFlags.Instance /*| BindingFlags.DeclaredOnly*/;

    private static bool InvalidField(FieldInfo field)
    //@formatter:off
        =>
        string.IsNullOrWhiteSpace(field.Name.TrimStart('_')) ||
        field.IsDefined(typeof(YamlIgnoreAttribute), true) ||
        field.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), true) ||
        field.Name.StartsWith('\u003c') ||
        field.Name.Contains("k__BackingField");
    //@formatter:on

    public static IEnumerable<(string Name, object? Value)> IterateMembers(Type type, object instance)
    {
        // Fields
        foreach (FieldInfo field in type.GetFields(Flags))
        {
            if (InvalidField(field))
                continue;

            string key = field.Name.TrimStart('_');
            var att = field.GetCustomAttribute<YamlMemberAttribute>();
            key = att != null
                ? field.GetCustomAttribute<YamlMemberAttribute>()?.Alias!
                : char.ToLowerInvariant(key[0]) + key[1..];

            yield return (key, field.GetValue(instance));
        }

        // Properties
        foreach (PropertyInfo prop in type.GetProperties(Flags))
        {
            if (!prop.CanRead)
                continue;

            string key = prop.Name.TrimStart('_');
            var att = prop.GetCustomAttribute<YamlMemberAttribute>();
            key = att != null
                ? prop.GetCustomAttribute<YamlMemberAttribute>()?.Alias!
                : char.ToLowerInvariant(key[0]) + key[1..];

            yield return (key, prop.GetValue(instance));
        }
    }
}