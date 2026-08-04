namespace SKSSL.Types;

/// <summary>
/// An internal collection of ulong flags for use as a Switchboard of boolean toggles based on index. 
/// </summary>
/// <remarks>
/// ===BitFlag Manual===<br/>
/// Bitflags are crazy things. It's a very efficient way to handle massive amounts of boolean checks.
/// GetFlag, SetFlag, and ClearFlag are all vital, and a chart with all the bitflags will be required.
/// This is limited to the size of the ulong data type, which frankly, is huge. Player-made conditions will
///  have to be something else entirely.
/// </remarks>
// ReSharper disable once UnusedType.Global
public class Switchboard
{
    private const int FlagCount = 100;

    /// 64 bits per ulong. The count formula [(FlagCount + 63) / 64] is for rounding the flag count to a power of 2. 
    private readonly ulong[] Flags = new ulong[(FlagCount + 63) / 64];

    private ref ulong Bucket(int idx) => ref Flags[idx >> 6];

    public bool GetFlag(int idx) => (Bucket(idx) & (1UL << (idx & 63))) != 0;

    /// <summary>
    /// Set a flag index as "true" within the switchboard. 
    /// </summary>
    /// <param name="idx">Index in Switchboard to flip based on 64 (0->63) indexing per long.</param>
    /// <example>
    /// SetFlag(0);   // bit 0 of Flags[0] <br/>
    /// SetFlag(1);   // bit 1 of Flags[0] <br/>
    /// SetFlag(63);  // bit 63 of Flags[0]<br/>
    /// SetFlag(64);  // bit 0 of Flags[1] <br/>
    /// SetFlag(65);  // bit 1 of Flags[1] <br/>
    /// </example>
    public void SetFlag(int idx) => Bucket(idx) |= 1UL << (idx & 63);

    public void ClearFlag(int idx) => Bucket(idx) &= ~(1UL << (idx & 63));
}