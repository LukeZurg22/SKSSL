using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using SKSSL.ECS;
using SKSSL.YAML;
using VYaml.Annotations;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace SKSSL;

public sealed class YamlComponentFormatter : IYamlFormatter<YamlComponent>
{
    public void Serialize(ref Utf8YamlEmitter emitter, YamlComponent obj, YamlSerializationContext context)
    {
        Type type = obj.GetType();

        emitter.BeginMapping();
        {
            emitter.WriteString("type");
            emitter.WriteString(obj.Type);

            foreach (var property in obj.Entries)
            {
                emitter.WriteString(property.Key);
                var value = property.Value?.ToString();
                if (value is null)
                {
                    emitter.WriteNull();
                }
                else if (value.Length == 0)
                {
                    emitter.WriteNull(); // treat empty as null
                }
                else
                {
                    //emitter.WriteScalar(i.Value);
                    //YamlSerializer.Serialize(ref emitter, i.Value, context.Options);
                    emitter.WriteString(value, ScalarStyle.Plain);
                }
            }
        }
        emitter.EndMapping();
    }

    public YamlComponent Deserialize(ref YamlParser parser, YamlDeserializationContext context)
        => DeserializeInternal(ref parser, context.Options);

    private static YamlComponent DeserializeInternal(ref YamlParser parser, YamlSerializerOptions options)
    {
        // Read entire mapping into an intermediate object
        var map = YamlSerializer.Deserialize<Dictionary<string, object>>(ref parser, options);

        if (map == null || !map.TryGetValue("type", out var typeObj) || typeObj == null)
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
        var yaml = YamlSerializer.Serialize(map, options);

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
            .Invoke(null, [yaml, options])!;
    }
}