// ReSharper disable UnusedMember.Global

using System;
using static SKSSL.ECS.UidPacker;

namespace SKSSL.ECS;

public readonly struct EntityUid : IEquatable<EntityUid>
{
    public readonly uint Value;

    public EntityUid(uint value) => Value = value;

    public EntityUid(int index, int generation) => Value = Pack(index, generation);

    public int Index => (int)(Value & 0xFFFF);
    public int Generation => (int)(Value >> 16);

    public bool Equals(EntityUid obj) => Value == obj.Value;
    public override bool Equals(object? obj) => obj is EntityUid other && Equals(other);
    public override int GetHashCode() => (int)Value;

    public static implicit operator uint(EntityUid uid) => uid.Value;
    public static implicit operator EntityUid(uint value) => new(value);

    public static bool operator ==(EntityUid left, EntityUid right) => left.Equals(right);
    public static bool operator !=(EntityUid left, EntityUid right) => !(left == right);
}