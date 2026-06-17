namespace SKSSL.ECS;

public static class UidPacker
{
    internal static uint Pack(int index, int generation) => (uint)(index & 0xFFFF) | ((uint)(generation & 0xFFFF) << 16);
    public static int UnpackIndex(uint value) => (int)(value & 0xFFFF);
    public static int UnpackGeneration(uint value) => (int)(value >> 16);
}