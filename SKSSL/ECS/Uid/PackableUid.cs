namespace SKSSL.ECS;

/// <summary>
/// Base schema for packable unique ID's.
/// </summary>
public interface PackableUid
{
    ulong Packed { get; }
    public int Index { get; }
    public int Generation { get; }

    /// Packs index (32-bit) + generation (32-bit) into a 64-bit value.
    public static ulong Pack(int index, int generation) => (uint)index | ((ulong)(uint)generation << 32);

    public static int UnpackIndex(PackableUid value) => (int)(value.Packed & 0xFFFFFFFFUL);
    public static int UnpackGeneration(PackableUid value) => (int)(value.Packed >> 32);
}