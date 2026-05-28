using System;
using SKSSL.ECS;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace SKSSL;

/// <summary>
/// Handle formatting for Lists of Prototypes.
/// </summary>
public sealed class PrototypeFormatter : IYamlFormatter<Prototype>
{
    public void Serialize(ref Utf8YamlEmitter emitter, Prototype value, YamlSerializationContext context)
    {
        emitter.BeginMapping();
        HierarchySerializerHelper.SerializeHierarchy(ref emitter, value, value.GetType(), context);
        emitter.EndMapping();
    }

    public Prototype Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        throw new NotImplementedException();
    }
}