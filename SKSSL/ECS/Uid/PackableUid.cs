using System;

namespace SKSSL.ECS;

public interface IUid<out TSelf> where TSelf : IUid<TSelf>
{
    ulong Packed { get; }
    public int Index { get; }
    public int Generation { get; }

    static abstract TSelf FromPacked(ulong packed);

    /// Packs index (32-bit) + generation (32-bit) into a 64-bit value.
    public static ulong Pack(int index, int generation) => (uint)index | ((ulong)(uint)generation << 32);

    public static int UnpackIndex(IUid<TSelf> value) => (int)(value.Packed & 0xFFFFFFFFUL);
    public static int UnpackGeneration(IUid<TSelf> value) => (int)(value.Packed >> 32);
}

/// Base schema for unique packable IDs.
/// Copy of <see cref="EntityUid"/>, but an elaborate generic Uid for use elsewhere; not registries.
public readonly struct PackableUid : IUid<PackableUid>, IEquatable<PackableUid>
{
    public ulong Packed { get; }

    public int Index => (int)(Packed & 0xFFFFFFFFUL);
    public int Generation => (int)(Packed >> 32);

    public PackableUid(ulong packed) => Packed = packed;
    public PackableUid(int index, int generation) => Packed = Pack(index, generation);

    public static ulong Pack(int index, int generation) =>
        (uint)index | ((ulong)(uint)generation << 32);

    // Value equality
    public bool Equals(PackableUid other) => Packed == other.Packed;
    public override bool Equals(object? obj) => obj is PackableUid other && Equals(other);
    public override int GetHashCode() => Packed.GetHashCode();

    public static bool operator ==(PackableUid left, PackableUid right) => left.Equals(right);
    public static bool operator !=(PackableUid left, PackableUid right) => !left.Equals(right);

    public override string ToString() => $"[{Index}:{Generation}][{Packed}]";

    public static PackableUid FromPacked(ulong packed) => new(packed);
    public T As<T>() where T : IUid<T> => T.FromPacked(Packed);
}