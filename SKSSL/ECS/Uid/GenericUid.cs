using System;
using static SKSSL.ECS.UidPacker;

namespace SKSSL.ECS;

public interface PackableUid
{
    ulong Packed { get; }
    public int Index { get; }
    public int Generation { get; }
}

/// <summary>
/// Copy of <see cref="EntityUid"/>, but an elaborate generic Uid for use elsewhere; not registries.
/// </summary>
public readonly struct GenericUid : PackableUid, IEquatable<GenericUid>
{
    public ulong Packed { get; }
    public int Index { get; }
    public int Generation { get; }
    public GenericUid(ulong packed) => Packed = packed;

    public GenericUid(int index, int generation)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(generation, 0);
        Packed = Pack(index, generation);
        Index = index;
        Generation = generation;
    }

    public bool Equals(GenericUid obj) => Packed == obj.Packed;
    public override bool Equals(object? obj) => obj is GenericUid other && Equals(other);
    public override int GetHashCode() => (int)Packed;

    public static implicit operator ulong(GenericUid uid) => uid.Packed;
    public static implicit operator GenericUid(ulong value) => new(value);

    public static bool operator ==(GenericUid left, GenericUid right) => left.Equals(right);
    public static bool operator !=(GenericUid left, GenericUid right) => !(left == right);
}