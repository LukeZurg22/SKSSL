using System;
using static SKSSL.ECS.UidPacker;

namespace SKSSL.ECS;

/// <summary>
/// Copy of <see cref="EntityUid"/>, but for statistics.
/// </summary>
public readonly struct StatisticUid : IEquatable<StatisticUid>
{
    public readonly uint Value;

    public StatisticUid(uint value) => Value = value;

    public StatisticUid(int index, int generation) => Value = Pack(index, generation);

    public int Index => (int)(Value & 0xFFFF);
    public int Generation => (int)(Value >> 16);

    public bool Equals(StatisticUid obj) => Value == obj.Value;
    public override bool Equals(object? obj) => obj is StatisticUid other && Equals(other);
    public override int GetHashCode() => (int)Value;

    public static implicit operator uint(StatisticUid uid) => uid.Value;
    public static implicit operator StatisticUid(uint value) => new(value);

    public static bool operator ==(StatisticUid left, StatisticUid right) => left.Equals(right);
    public static bool operator !=(StatisticUid left, StatisticUid right) => !(left == right);
}