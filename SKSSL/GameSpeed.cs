namespace SKSSL;

/// <summary>
/// For forced arbitrary speed modes.
/// </summary>
public enum GameSpeed : byte
{
    /// Speed 0 (None)
    Paused,

    /// Speed 1 (Very Slow)
    Mach1, // Aircraft < 0.8

    /// Speed 2 (Slow)
    Mach2, // Supersonic

    /// Speed 3 (Normal)
    Mach3, // Supersonic

    /// Speed 4 (Fast)
    Mach4, // Supersonic

    /// Speed 5 (Very Fast)
    Mach5, // Hypersonic
}