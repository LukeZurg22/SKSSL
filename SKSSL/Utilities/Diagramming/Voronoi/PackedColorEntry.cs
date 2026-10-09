namespace SKSSL.Utilities.Voronoi;

public record struct PackedColorEntry
{
    public uint PackedColor;
    public uint CellIdPlusOne; // Zero means the slot is empty.
}