using System;
using SKSSL.ECS;
using VYaml.Emitter;
using VYaml.Parser;
using VYaml.Serialization;

namespace SKSSL;


public sealed class ComponentFormatter : IYamlFormatter<Component>
{
    public void Serialize(ref Utf8YamlEmitter emitter, Component value, YamlSerializationContext context)
    {
        throw new NotImplementedException();
    }

    public Component Deserialize(ref YamlParser parser, YamlDeserializationContext context)
    {
        throw new NotImplementedException();
    }
}