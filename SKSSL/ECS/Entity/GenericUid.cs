using System;
using static SKSSL.ECS.UidPacker;

namespace SKSSL.ECS;

public interface PackableUid
{
    uint Value { get; }
    public int Index { get; }
    public int Generation { get; }
}

/// <summary>
/// Copy of <see cref="EntityUid"/>, but an elaborate generic Uid for use elsewhere; not registries.
/// </summary>
public readonly struct GenericUid : PackableUid, IEquatable<GenericUid>
{
    public GenericUid(uint value) => Value = value;

    public GenericUid(int index, int generation) => Value = Pack(index, generation);

    public uint Value { get; }
    public int Index => UnpackIndex(this);
    public int Generation => UnpackGeneration(this);

    public bool Equals(GenericUid obj) => Value == obj.Value;
    public override bool Equals(object? obj) => obj is GenericUid other && Equals(other);
    public override int GetHashCode() => (int)Value;

    public static implicit operator uint(GenericUid uid) => uid.Value;
    public static implicit operator GenericUid(uint value) => new(value);

    public static bool operator ==(GenericUid left, GenericUid right) => left.Equals(right);
    public static bool operator !=(GenericUid left, GenericUid right) => !(left == right);
}