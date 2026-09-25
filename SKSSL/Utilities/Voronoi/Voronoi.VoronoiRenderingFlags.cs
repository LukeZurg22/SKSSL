using System;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    [Flags]
    // ReSharper disable UnusedMember.Global
    public enum VoronoiRenderingFlags : byte
    {
        //@formatter:off
        /// Used as a "render nothing" and a null toggle.
        None      = 0,
        /// Draw dots.
        Points     = 1 << 0,
        /// Draw "proper" voronoi cells.
        Cells      = 1 << 1,
        /// Draw edges between cells.
        Edges     = 1 << 2,
        /// Draw the triangles that make up each cell.
        Triangles = 1 << 3,
        
        CellsAndEdges = Cells | Edges,
        All = Points | Cells | Edges | Triangles
        //@formatter:on
    }
}