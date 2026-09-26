namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    public enum ColorMode : byte
    {
        Unified,
        Unique,

        /// Half deterministic color creation, which defaults to unique during conflicts.
        Semi_Deterministic_Unique,
        Deterministic_Random,
        Deterministic_Lines,
        Random,
    }
}