using System;
using System.IO;
using static SKSSL.Loc;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

// ReSharper disable MemberCanBePrivate.Global

namespace SKSSL;

#region LocKey

/// <summary>
/// Localization wrapper around a Key String.
/// </summary>
public readonly record struct LocKey
{
    public readonly string Key;

    public LocKey(string key) => Key = key;

    public string Resolve() => Get(Key);

    public override string ToString() => Resolve();
}

/// Simple string converter for Yaml Parser.
public sealed class LocIdYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(LocKey);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => new LocKey(parser.Consume<Scalar>().Value);

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is LocKey loc)
            emitter.Emit(new Scalar(loc.Key));
    }
}

#endregion

#region Handle

/// <summary>
/// Localization wrapper around a Handle String.
/// </summary>
public readonly record struct Handle
{
    public readonly string Value;

    public Handle(string value) => Value = value;

    public string Unwrap() => Value;
}

/// Simple string converter for Yaml Parser.
public sealed class HandleYamlConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(Handle);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        => new Handle(parser.Consume<Scalar>().Value);

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        if (value is Handle handle)
            emitter.Emit(new Scalar(handle.Value));
    }
}

#endregion

#region FileInfo

/// Simple string converter for Yaml Parser.
public sealed class FileInfoYamlConverter : IYamlTypeConverter
{
    private readonly string _root;

    public FileInfoYamlConverter(string root) => _root = root;

    public bool Accepts(Type type) => type == typeof(FileInfo);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var scalar = parser.Consume<Scalar>();
        return new FileInfo(Path.Combine(_root, scalar.Value));
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
    {
        // Simply writes nothing.
        if (value == null)
            return;
        
        var file = (FileInfo)value;
        string relative = Path.GetRelativePath(
            _root,
            file.FullName
        );
        emitter.Emit(new Scalar(relative));
    }
}

#endregion