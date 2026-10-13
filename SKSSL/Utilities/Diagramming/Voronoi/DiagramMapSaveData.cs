using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

/// Reconstruction data to allow a diagram to be reconstructed.
public class DiagramMapSaveData
{
    public int Width { get; set; } // Width of Image (not window)
    public int Height { get; set; } // Height of Image (not window)

    // WIP: Who cares about the source mode? public VoronoiSourceMode SourceMode { get; set; }
    public VoronoiBoundaryMode BoundaryMode { get; set; }

    public Vector2[] Sites { get; set; }
    public Color[] CellIdToCellColor { get; set; }
    public Color[]? OverlayColors { get; set; }
    public VoronoiOptions VoronoiOptions { get; set; }

    // WIP: Serialize the full raster as a .png to read.
    /// The most "expensive" data that needs binary serialization.
    public uint[] RawPixelData { get; set; }

    /// Serialize as a separate, user-friendly map that maps CELL ID -> CELL COLOR.
    Dictionary<int, uint> CellIdToColor { get; set; }

    // WIP: Reconstruct this in LOAD.
    //public int[] PixelToCellId { get; set; }


    // WIP: Note fields from DiagramMap To-> here. Map IMAGE data may need to be serialized to a binary file,
    //  ...perhaps even compressed?
}