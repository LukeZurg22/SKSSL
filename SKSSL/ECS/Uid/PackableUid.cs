using System;

namespace SKSSL.ECS;

/// <summary>
/// A generic but Unique Identifier best used for tracking instanced objects.
/// </summary>
/// <typeparam name="TSelf">
/// Self-reflected implementation that implements this interface for casting purposes.
/// </typeparam>
/// <example>
/// ... struct <see cref="EntityUid"/> : IUid&lt;<see cref="EntityUid"/>&gt;, IEquatable&lt;<see cref="EntityUid"/>&gt;
/// </example>
/// <seealso cref="PackableUid"/>
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

/// Base schema for unique packable IDs. An elaborate generic Uid for use anywhere.
/// Implements <see cref="IUid{TSelf}"/>.
/// <seealso cref="EntityUid"/>
public readonly struct PackableUid : IUid<PackableUid>, IEquatable<PackableUid>
{
    public ulong Packed { get; }

    public int Index => (int)(Packed & 0xFFFFFFFFUL);
    public int Generation => (int)(Packed >> 32);

    public PackableUid(ulong packed) => Packed = packed;
    public PackableUid(int index, int generation) => Packed = Pack(index, generation);

    /// Backs this Uid into a compact <see cref="ulong"/>.
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

    /// Cast a Uid as another type of <see cref="IUid{TSelf}"/> implementation.
    public T As<T>() where T : IUid<T> => T.FromPacked(Packed);
}