using System;

namespace SKSSL.Types;

/// <summary>
/// An internal collection of ulong flags for use as a Switchboard of boolean toggles based on index.
/// This board uses zero-based 64-bit indexing.
/// </summary>
/// <remarks>
/// ===BitFlag Manual===<br/>
/// Bitflags are crazy things. It's a very efficient way to handle massive amounts of boolean checks.
/// GetFlag, SetFlag, and ClearFlag are all vital, and a chart with all the bitflags will be required.
/// This is limited to the size of the ulong data type, which frankly, is huge. Player-made conditions will
/// have to be something else entirely.
/// </remarks>
/// <example>
/// <code>
/// // Create an instance of the Switchboard and utilize the Functions provided.
/// Switchboard myBelovedSwitchboard = new Switchboard();
/// myBelovedSwitchboard.SetFlag(1);
/// myBelovedSwitchboard.GetFlag(1);
/// myBelovedSwitchboard.ClearFlag(1);
/// </code>
/// One should keep a record or some sort of notes on the Switchboard's contents if they intend to use them for
/// things such as story or plot boolean trackers.<br/>
///<br/>
/// === Story Switchboard ===<br/>
///    INDEX NAME<br/>
/// /* 0->63 <br/>
///  * 000 The Adventure Failed<br/>
///  * 001 The Adventure Begins<br/>
/// ...<br/>
///  * 063 The Great Sword is Destroyed
/// */
/// /* ... (And so forth.)
/// </example>
// ReSharper disable once UnusedType.Global
public class Switchboard
{
    private const int FlagCount = 100;

    /// 64 bits per ulong. The count formula [(FlagCount + 63) / 64] is for rounding the flag count to a power of 2.
    private readonly ulong[] Flags = new ulong[(FlagCount + 63) / 64];

    private static void ValidateIndex(int idx)
    {
        if ((uint)idx >= FlagCount) throw new ArgumentOutOfRangeException(nameof(idx));
    }

    private ref ulong Bucket(int idx)
    {
        // Common call by all functions present in this class. Best to validate here rather than repeat each call.
        ValidateIndex(idx);
        return ref Flags[idx >> 6];
    }

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