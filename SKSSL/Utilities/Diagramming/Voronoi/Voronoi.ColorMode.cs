namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    /// <summary>
    /// The "Randomness" of cell coloring in a Voronoi diagram. Only accounts for "Raw" cell colors.
    /// </summary>
    public enum ColorMode : byte
    {
        Unified,
        Pure_Random,

        /// Half deterministic color creation, which defaults to unique during conflicts.
        Semi_Random,
        Deterministic_HashRandom,
        Deterministic_Hash,
        Random,

        /// Directly-encoded integer ID to Color. Causes maps to have flowing, but unique colors.
        /// Upper limit 16+ million.
        Encoded
    }
}