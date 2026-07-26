// ReSharper disable UnusedMember.Global

using System;
using static SKSSL.ECS.PackableUid;

namespace SKSSL.ECS;

/// <summary>
/// Specialized PackableUid for exclusive use with entities. This is copied from <see cref="PackableUid"/>.
/// </summary>
public readonly struct EntityUid : IUid<EntityUid>, IEquatable<EntityUid>
{
    public ulong Packed { get; }

    public EntityUid(ulong packed) => Packed = packed;
    public EntityUid(int index, int generation) => Packed = Pack(index, generation);

    public int Index => IUid<EntityUid>.UnpackIndex(this);
    public int Generation => IUid<EntityUid>.UnpackGeneration(this);
    public static EntityUid FromPacked(ulong packed) => new(packed);

    public bool Equals(EntityUid obj) => Packed == obj.Packed;
    public override bool Equals(object? obj) => obj is EntityUid other && Equals(other);
    public override int GetHashCode() => (int)Packed;

    public static implicit operator ulong(EntityUid uid) => uid.Packed;
    public static implicit operator EntityUid(ulong value) => new(value);

    public static bool operator ==(EntityUid left, EntityUid right) => left.Equals(right);
    public static bool operator !=(EntityUid left, EntityUid right) => !(left == right);
    public T As<T>() where T : IUid<T> => T.FromPacked(Packed);
}