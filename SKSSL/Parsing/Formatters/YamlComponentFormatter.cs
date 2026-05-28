using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SKSSL.ECS;
using SKSSL.YAML;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace SKSSL;

public sealed class YamlComponentFormatter : IYamlFormatter<YamlComponent>
{
    public void Serialize(ref Utf8YamlEmitter emitter, YamlComponent obj, YamlSerializationContext context)
    {
        emitter.BeginMapping();
        {
            emitter.WriteString("type");
            emitter.WriteString(obj.Type);

            foreach (var kvp in obj.Entries)
            {
                if (kvp.Key == "type") continue; // avoid collision

                emitter.WriteString(kvp.Key);
                SerializeValue(ref emitter, kvp.Value, context);
            }
        }
        emitter.EndMapping();
    }


    /// Converts any value to a reasonable YAML-compatible string representation.
    /// Handles null, primitives, strings, Vectors, common MonoGame types, etc.
    private static void SerializeValue(ref Utf8YamlEmitter emitter, object? value, YamlSerializationContext context)
    {
        if (value == null)
        {
            emitter.WriteNull();
            return;
        }

        switch (value)
        {
            case string str:
                emitter.WriteString(str, ScalarStyle.Plain);
                break;

            case bool b:
                emitter.WriteBool(b);
                break;

            case int i:
                emitter.WriteInt32(i);
                break;

            case long l:
                emitter.WriteInt64(l);
                break;

            case float f:
                emitter.WriteFloat(f);
                break;

            case double d:
                emitter.WriteDouble(d);
                break;

            case IList list: // arrays and lists
                emitter.BeginSequence();
                foreach (var item in list)
                    SerializeValue(ref emitter, item, context);
                emitter.EndSequence();
                break;

            case IDictionary dict: // nested objects
                emitter.BeginMapping();
                foreach (DictionaryEntry entry in dict)
                {
                    emitter.WriteString(entry.Key.ToString() ?? "");
                    SerializeValue(ref emitter, entry.Value, context);
                }

                emitter.EndMapping();
                break;

            // Common MonoGame types
            case Microsoft.Xna.Framework.Vector2 vector2:
                emitter.WriteString($"[{vector2.X}, {vector2.Y}]", ScalarStyle.Plain);
                break;
            case Microsoft.Xna.Framework.Vector3 vector3:
                emitter.WriteString($"[{vector3.X}, {vector3.Y}, {vector3.Z}]", ScalarStyle.Plain);
                break;
            case Microsoft.Xna.Framework.Color color: // Convert to 0xHEX.
                emitter.WriteString($"0x{color.PackedValue:X8}", ScalarStyle.Plain);
                break;

            default:
                // Fallback: try VYaml's built-in serializer for complex objects.
                try
                {
                    YamlSerializer.Serialize(ref emitter, value, context.Options);
                }
                catch
                {
                    // Last resort
                    emitter.WriteString(value.ToString() ?? "", ScalarStyle.Plain);
                }

                break;
        }
    }

    // WIP: CLEANING DESERIAL
    public YamlComponent Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        // Read entire mapping into an intermediate object
        var map = YamlSerializer.Deserialize<Dictionary<string, object>>(ref parser, context.Options);

        if (!map.TryGetValue("type", out var typeObj))
            throw new YamlSerializerException("Component is missing required 'type' field.");

        string componentName = typeObj.ToString() ?? string.Empty;
        string fullTypeName =
            ComponentTypeHelper.GetFullComponentTypeName(componentName);

        var componentType =
            ComponentRegistry.RegisteredHandleComponentTypesDictionary
                .FirstOrDefault(t =>
                    t.Key.Equals(fullTypeName,
                        StringComparison.OrdinalIgnoreCase) ||
                    t.Key.Equals(componentName,
                        StringComparison.OrdinalIgnoreCase));

        if (string.IsNullOrWhiteSpace(componentType.Key))
            throw new YamlSerializerException($"Unknown component type: {componentName}");

        // Re-serialize dictionary back into yaml
        var yaml = YamlSerializer.Serialize(map, context.Options);

        // Deserialize into the concrete component type
        parser = new YamlParser(new ReadOnlySequence<byte>(yaml));
        return (YamlComponent?)typeof(YamlSerializer)
            .GetMethods()
            .First(m =>
                m.Name == nameof(YamlSerializer.Deserialize) &&
                m.IsGenericMethod &&
                m.GetParameters().Length == 2 &&
                m.GetParameters()[0].ParameterType == typeof(ReadOnlyMemory<byte>)
            )
            .MakeGenericMethod(componentType.Value)
            .Invoke(null, [yaml, context.Options])!;
    }
}