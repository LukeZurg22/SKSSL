using System;
using System.Linq;
using System.Reflection;
using SKSSL.ECS;
using SKSSL.YAML;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace SKSSL;

public sealed class EntityFormatter : IYamlFormatter<Entity>
{
    public void Serialize(ref Utf8YamlEmitter emitter, Entity value, YamlSerializationContext context)
    {
        emitter.BeginMapping();

        Type type = value.GetType();
        Type baseType = typeof(Prototype);

        // Identity (important for reconstruction)
        emitter.WriteString("type");
        emitter.WriteString(type.Name);

        // 1. Prototype fields
        SerializeTypeFields(ref emitter, value, baseType, context);

        // 2. Entity-specific fields
        SerializeTypeFields(ref emitter, value, type, context, baseType);

        emitter.EndMapping();
    }

    public Entity Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        var prototype = new Entity();

        parser.Read(); // MappingStart
        while (parser.CurrentEventType != ParseEventType.MappingEnd)
        {
            string key = parser.GetScalarAsString()!;
            parser.Read();

            if (key == "components")
            {
                parser.Read(); // SequenceStart

                while (parser.CurrentEventType != ParseEventType.SequenceEnd)
                {
                    var entry = YamlSerializer.Deserialize<YamlComponent>(ref parser, context.Options);

                    string fullType =
                        ComponentTypeHelper.GetFullComponentTypeName(entry.Type);

                    var componentType =
                        ComponentRegistry.RegisteredHandleComponentTypesDictionary
                            .First(t =>
                                t.Key.Equals(fullType, StringComparison.OrdinalIgnoreCase) ||
                                t.Key.Equals(entry.Type, StringComparison.OrdinalIgnoreCase));

                    var component =
                        Activator.CreateInstance(componentType.Value)
                        ?? throw new Exception($"Cannot create {entry.Type}");

                    foreach (var kv in entry.Entries)
                    {
                        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
                        FieldInfo? field = componentType.Value.GetField(kv.Key, flags);

                        if (field == null)
                            continue;

                        field.SetValue(component, Convert.ChangeType(kv.Value, field.FieldType));
                    }

                    prototype.YamlComponents.Add((YamlComponent)component);
                }

                parser.Read(); // SequenceEnd
            }
            else
            {
                parser.SkipCurrentNode();
            }
        }

        parser.Read(); // MappingEnd

        return prototype;
    }


    private static void SerializeTypeFields(
        ref Utf8YamlEmitter emitter,
        object obj,
        Type type,
        YamlSerializationContext context,
        Type? skipBaseType = null)
    {
        while (type != skipBaseType)
        {
            foreach ((string Name, object? Value) d in  FormatterFieldHelper.IterateMembers(type, obj))
            {
                /*emitter.WriteString(field.Name.TrimStart('_'));
                var value = field.GetValue(obj);
                switch (value)
                {
                    case null: continue;
                    case List<YamlComponent> list: // Manually serialize list.
                    {
                        foreach (YamlComponent c in list)
                            YamlSerializer.Serialize(ref emitter, c, context.Options);
                        break;
                    }
                    default:
                        YamlSerializer.Serialize(ref emitter, value, context.Options);
                        break;
                }*/

            }
            type = type.BaseType!;
        }
    }
}