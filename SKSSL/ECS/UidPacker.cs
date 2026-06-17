namespace SKSSL.ECS;

internal static class UidPacker
{
    internal static uint Pack(int index, int generation) => (uint)(index & 0xFFFF) | ((uint)(generation & 0xFFFF) << 16);
    internal static int UnpackIndex(uint value) => (int)(value & 0xFFFF);
    internal static int UnpackGeneration(uint value) => (int)(value >> 16);
}