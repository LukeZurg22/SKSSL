namespace SKSSL.ECS;

internal static class UidPacker
{
    /// Packs index (32-bit) + generation (32-bit) into a 64-bit value.
    internal static ulong Pack(int index, int generation) => (uint)index | ((ulong)(uint)generation << 32);
    internal static int UnpackIndex(PackableUid value) => (int)(value.Packed & 0xFFFFFFFFUL);
    internal static int UnpackGeneration(PackableUid value) => (int)(value.Packed >> 32);
}