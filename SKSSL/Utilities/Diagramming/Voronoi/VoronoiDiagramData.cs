using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

/// <summary>
/// A snapshot of the most recent generation. Arrays and textures are owned by Voronoi and
/// reused on later generations; consume them before regenerating or copy the arrays.
/// </summary>
public sealed class VoronoiDiagramData
{
    internal VoronoiDiagramData(
        int width, int height, Vector2[] sites, Color[] cellIdToCellColor, int[] pixelToCellId,
        Texture2D cellIdTexture, Color[]? overlayColors,
        VoronoiSourceMode sourceMode, VoronoiBoundaryMode boundaryMode, VoronoiOptions voronoiOptions)
    {
        Width = width;
        Height = height;
        Sites = sites;
        CellIdToCellColor = cellIdToCellColor;
        PixelToCellId = pixelToCellId;
        CellIdTexture = cellIdTexture;
        OverlayColors = overlayColors;
        SourceMode = sourceMode;
        BoundaryMode = boundaryMode;
        VoronoiOptions = voronoiOptions;
    }

    public int Width { get; }
    public int Height { get; }
    public Vector2[] Sites { get; }

    /// <summary>Zero-based site ID per pixel; -1 means transparent/unassigned.</summary>
    public int[] PixelToCellId { get; }

    public Color[] CellIdToCellColor { get; set; }

    /// <summary>RGBA8 texture encoding site ID + 1 in RGB; alpha zero is the background.</summary>
    public Texture2D CellIdTexture { get; }

    public Color[]? OverlayColors { get; set; }

    public VoronoiSourceMode SourceMode { get; }
    public VoronoiBoundaryMode BoundaryMode { get; }
    public VoronoiOptions VoronoiOptions { get; set; }
}