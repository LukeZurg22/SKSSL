using System;
using System.IO;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using static SKSSL.Loc;

// ReSharper disable MemberCanBePrivate.Global

namespace SKSSL.Serializing;

#region LocKey

/// <summary>
/// Localization wrapper around a Key String.
/// </summary>
public readonly struct LocKey : IEquatable<LocKey>
{
    public readonly string Key;

    public LocKey(string key) => Key = key;

    public string Resolve() => Get(Key);

    public override string ToString() => Resolve();
    public bool Equals(LocKey other) => Key.Equals(other.Key);
    public override bool Equals(object? obj) => obj is LocKey handle && Equals(handle);
    public override int GetHashCode() => Key.GetHashCode();
    public static bool operator ==(LocKey left, LocKey right) => left.Equals(right);
    public static bool operator !=(LocKey left, LocKey right) => !(left == right);
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
public readonly struct Handle : IEquatable<Handle>
{
    public readonly string Value;

    public Handle(string value) => Value = value;

    public string Unwrap() => ToString().TrimEnd('\r', '\n').Trim();
    public override string ToString() => Value;
    public bool Equals(Handle other) => Value.Equals(other.Value);
    public override bool Equals(object? obj) => obj is Handle handle && Equals(handle);
    public override int GetHashCode() => Value.GetHashCode();
    public static bool operator ==(Handle left, Handle right) => left.Equals(right);
    public static bool operator !=(Handle left, Handle right) => !(left == right);
    public static implicit operator Handle(string value) => new(value);
    public static implicit operator string(Handle value) => value.Value;

    // ReSharper disable once UnusedMember.Global
    public bool IsNullOrEmpty() => string.IsNullOrEmpty(Value);

    // ReSharper disable once UnusedMember.Global
    public bool IsNullOrWhiteSpace() => string.IsNullOrWhiteSpace(Value);
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