namespace SKSSL.Utilities.Voronoi;

public enum CellIdSortingMode : byte
{
    /// Completely random ID assignment.
    Random,

    /// By far the -weirdest- one that makes cellar "clumps" of ids.
    SpatialConnectivity,

    /// Semi-traditional "linear" IDs in rows.
    TopLeftToBottomRight_Linear_v1,

    /// Focuses ids around the centre of the entire diagram.
    TopLeftToBottomRight_Circular_v1,

    /// Uses a provided height map to partially distribute cell ids using the Hilbert index. Not the most performant.
    HeightMapExperimental
}