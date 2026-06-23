using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
// ReSharper disable PropertyCanBeMadeInitOnly.Global

namespace SKSSL.ECS;

/// Structure for component information contained in YAML files.
[JsonObject]
public partial class ComponentYaml : IYamlConvertible
{
    // e.g., "RenderableComponent" but named "Renderable"; it's stripped of the "Component" suffix.
    [YamlMember(Alias = "type")]
    [JsonProperty(PropertyName = "Type")]
    public string Type { get; set; }

    // Dictionary for flexible fields (for varied components)
    /// <summary>
    /// Variable Fields contained in the record that defines the component.
    /// Private code will require provided component documentation for user-defined entities.
    /// </summary>
    /// <remarks>
    /// It's funky. Field names should be about as 1:1 to the actual component's fields.
    /// As far as I know, it's case sensitive.
    /// </remarks>
    public Dictionary<string, object?> Entries { get; set; } = new();

    public void Read(
        IParser parser,
        Type expectedType,
        ObjectDeserializer nestedObjectDeserializer)
    {
        var values = nestedObjectDeserializer(typeof(Dictionary<object, object?>));

        if (values is not Dictionary<object, object?> nestedObjectDictionary)
            return;

        Entries.Clear();

        foreach (var pair in nestedObjectDictionary)
        {
            var key = pair.Key.ToString() ?? "";

            if (key == "type")
                Type = pair.Value?.ToString() ?? "";
            else
                Entries[key] = pair.Value;
        }
    }

    public void Write(IEmitter emitter, ObjectSerializer nestedObjectSerializer)
    {
        emitter.Emit(new MappingStart());

        emitter.Emit(new Scalar("type"));
        emitter.Emit(new Scalar(Type));

        foreach (var entry in Entries)
        {
            emitter.Emit(new Scalar(entry.Key));
            nestedObjectSerializer(entry.Value);
        }

        emitter.Emit(new MappingEnd());
    }
}