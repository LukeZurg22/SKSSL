using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SKSSL.ECS;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

namespace SKSSL.Serializing;

/// <summary>
/// Lifted from "Consequaintances", this is an incredibly simple loader for Json files
/// using system <see cref="System.Text.Json"/>.
/// </summary>
public class SerializerDefaultJson : ISerializer
{
    // FROM THE CONSEQUAINTANCE PROJECT.

    private readonly JsonSerializerOptions _options = new() { WriteIndented = true };

    public string Serialize<T>(T obj) where T : class => JsonSerializer.Serialize(obj, _options);

    public T? Deserialize<T>(string serialized) => JsonSerializer.Deserialize<T>(serialized, _options);

    /// Loads json file at path and returns a list of objects, even if there is only one entry.
    public List<Prototype> DeserializePrototypes(string text, string trace = "", params Type[] types)
    {
        List<Prototype> output = [];
        // Extracting the type annotated.
        using JsonDocument doc = JsonDocument.Parse(text);
        var n = doc.RootElement.GetProperty("type").GetString()!;
        Type? target = types.FirstOrDefault(t => t.Name.Equals(n));
        if (target == null)
        {
            Log($"Failed to deserialize {trace} as JSON!");
            return [];
        }

        // Deserialize & populate the output list as root Prototype type.
        IList protoList = DeserializeJsonAsType(text, target);
        foreach (var proto in protoList) output.Add((proto as Prototype)!);
        return output;
    }

    #region Helpers

    private static IList DeserializeJsonAsType(string text, Type type)
    {
        using JsonDocument doc = JsonDocument.Parse(text);

        Type listType = typeof(List<>).MakeGenericType(type);
        var list = (IList)Activator.CreateInstance(listType)!;

        switch (doc.RootElement.ValueKind)
        {
            case JsonValueKind.Array:
            {
                var deserialized = (IEnumerable?)JsonSerializer.Deserialize(text, listType);
                if (deserialized != null)
                    foreach (var item in deserialized)
                        list.Add(item);

                break;
            }

            case JsonValueKind.Object:
            {
                var deserialized = JsonSerializer.Deserialize(text, type);
                if (deserialized != null)
                    list.Add(deserialized);
                break;
            }
        }

        return list;
    }

    #endregion
}