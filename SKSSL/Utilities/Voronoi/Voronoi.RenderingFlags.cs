using System;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    [Flags]
    // ReSharper disable UnusedMember.Global
    public enum VoronoiRenderingFlags : byte
    {
        //@formatter:off
        /// Draw "proper" voronoi cells. Everything else is overlaid.
        Cells      = 1 << 0,
        /// Draw dots.
        Points     = 1 << 1,
        /// Draw edges between cells.
        Edges     = 1 << 2,
        /// Draw the triangles that make up each cell.
        Triangles = 1 << 3,
        
            // Grouped Flags for Simplicity 
            CellsPoints = Cells | Points,
            CellsEdges = Cells | Edges,
            EdgesPoints = Edges | Points,
            CellsEdgesPoints = Cells | Edges | Points,
            All = Points | Cells | Edges | Triangles
        //@formatter:on
    }
}