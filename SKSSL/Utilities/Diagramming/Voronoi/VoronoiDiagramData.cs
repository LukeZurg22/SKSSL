using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public readonly ref struct VoronoiDiagramData
{
    public readonly Texture2D PixelMap;
    public readonly Texture2D? OverlayMap;
    public readonly Color[] CellRawColors;
    public readonly PackedColorEntry[] ColorToIdLookup;
    public readonly int ColorToIdMask;
    public readonly DiagramSettings Settings;
    public readonly int CellCount;
    
    public VoronoiDiagramData(
        DiagramSettings settings,
        Color[] cellRawColors,
        PackedColorEntry[] colorToIdLookup,
        int colorToIdMask,
        Texture2D pixelMap,
        Texture2D? overlayMap)
    {
        Settings = settings;
        CellRawColors = cellRawColors;
        ColorToIdLookup = colorToIdLookup;
        ColorToIdMask = colorToIdMask;
        OverlayMap = overlayMap;
        PixelMap = pixelMap;
    }
}