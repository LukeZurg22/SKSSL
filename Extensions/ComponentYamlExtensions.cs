using SKSSL.ECS;
using SKSSL.YAML;
using System.Reflection;
using YamlDotNet.Serialization;

namespace SKSSL.Extensions;
public static class ComponentYamlExtensions
{
    public static ComponentYaml ToComponentYaml(this ISKComponent componentInterface)
    {
        ArgumentNullException.ThrowIfNull(componentInterface);

        Type concreteType = componentInterface.GetType();
        var fieldPairs = new Dictionary<string, object>();

        // Get all instance fields (public + non-public)
        var fields = concreteType.GetFields(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        foreach (FieldInfo field in fields)
        {
            // Skip fields marked with unwanted attributes that the yaml parser ignores.
            // If the yaml parser can ignore it, then why can't we?
            if (field.IsDefined(typeof(YamlIgnoreAttribute), inherit: true))
                continue;
            object? value = field.GetValue(componentInterface);
            if (value is null)
                continue;
            
            string valueString = ValueToYamlString(value);

            // Use field name as key
            string key = field.Name.TrimStart('_');

            fieldPairs[key] = valueString;
        }

        return new ComponentYaml
        {
            Type = concreteType.Name, // or FullName if you need namespace
            Fields = fieldPairs
        };
    }

    /// <summary>
    /// Converts any value to a reasonable YAML-compatible string representation.
    /// Handles null, primitives, strings, Vectors, common MonoGame types, etc.
    /// </summary>
    private static string ValueToYamlString(object value)
    {
        Type type = value.GetType();

        // String: quote it
        if (type == typeof(string))
            return $"\"{value}\"";

        // Common MonoGame types
        if (type == typeof(Microsoft.Xna.Framework.Vector2))
        {
            var v = (Microsoft.Xna.Framework.Vector2)value;
            return $"{v.X}, {v.Y}";
        }
        if (type == typeof(Microsoft.Xna.Framework.Vector3))
        {
            var v = (Microsoft.Xna.Framework.Vector3)value;
            return $"{v.X}, {v.Y}, {v.Z}";
        }
        if (type == typeof(Microsoft.Xna.Framework.Color))
        {
            var c = (Microsoft.Xna.Framework.Color)value;
            return c.PackedValue.ToString("X8"); // or use Name if known
        }

        // Enums: use name
        if (type.IsEnum)
            return value.ToString()!;

        // Primitives: just ToString()
        if (type.IsPrimitive || type == typeof(decimal))
            return value.ToString()!.ToLowerInvariant(); // e.g., true/false instead of True/False

        // Fallback: ToString(), or type name if not helpful
        string str = value.ToString()!;
        return str != type.ToString() ? str : $"~{type.Name}~"; // mark complex objects
    }
}