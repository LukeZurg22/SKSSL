using System;
using static SKSSL.Loc;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

// ReSharper disable MemberCanBePrivate.Global

namespace SKSSL;

public readonly record struct LocKey
{
    public readonly string Key;

    public LocKey(string key) => Key = key;

    public string Resolve() => Get(Key);

    public override string ToString() => Resolve();
}

/// Simple string converter for Yaml Parser.
public class LocIdYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type)
        => type == typeof(LocKey);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => new LocKey(parser.Consume<Scalar>().Value);

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is LocKey loc)
            emitter.Emit(new Scalar(loc.Key));
    }
}