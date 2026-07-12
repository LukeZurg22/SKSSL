// ReSharper disable UnusedMember.Global

using System;
using static SKSSL.ECS.UidPacker;

namespace SKSSL.ECS;

/// <summary>
/// Specialized PackableUid for exclusive use with entities. This is copied from <see cref="GenericUid"/>.
/// </summary>
public readonly struct EntityUid : PackableUid, IEquatable<EntityUid>
{
    public uint Value { get; }

    public EntityUid(uint value) => Value = value;

    public EntityUid(int index, int generation) => Value = Pack(index, generation);

    public int Index => UnpackIndex(this);
    public int Generation => UnpackGeneration(this);

    public bool Equals(EntityUid obj) => Value == obj.Value;
    public override bool Equals(object? obj) => obj is EntityUid other && Equals(other);
    public override int GetHashCode() => (int)Value;

    public static implicit operator uint(EntityUid uid) => uid.Value;
    public static implicit operator EntityUid(uint value) => new(value);

    public static bool operator ==(EntityUid left, EntityUid right) => left.Equals(right);
    public static bool operator !=(EntityUid left, EntityUid right) => !(left == right);
    public static EntityUid FromPackableUid(PackableUid uid) => new(uid.Value);
}