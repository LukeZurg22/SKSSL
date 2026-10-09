/*using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class CellMap
{
    private readonly HashSet<uint> _markedCells = []; // Collected cells to interact with.

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
#pragma warning disable CS0169 // Field is never used
    private Texture2D? _highlightMask; // Mask for highlighting cells.
#pragma warning restore CS0169 // Field is never used
    // TODO: Highlights versus demarcations.

    #region Cell Marking

    /// <summary>
    /// Using a position on a Voronoi diagram, attempts to get a cell and add it to a marked-cells list. 
    /// </summary>
    /// <param name="point"></param>
    // ReSharper disable once UnusedMember.Global
    public void MarkCell(System.Drawing.Point point)
    {
        if (!TryGetCellAt(point, out VoronoiCell? cell))
            return;

        MarkCell(cell.ID);
    }

    /// <summary>
    /// Add a cell instance ID into a list of marked-cells. 
    /// </summary>
    /// <param name="cell"></param>
    // ReSharper disable once UnusedMember.Global
    public void MarkCell(uint cell)
    {
        //if (_cellsBySiteId.ContainsKey(cell))
        _markedCells.Add(cell);
    }

    public void ClearMarkedCells() => _markedCells.Clear();

    #endregion

    #region Coloring

    /// <summary>
    /// Calls <see cref="ChangeCellColors"/> using the cell ids marked by <see cref="MarkCell(uint)"/>.
    /// </summary>
    /// <param name="color">Color to replace cells.</param>
    /// <param name="blankColor"></param>
    /// <param name="flattenOthers">Set all other non-marked cells to an alternate color.</param>
    /// <param name="clear">Clear internally marked cells. False by default.</param>
    // ReSharper disable once UnusedMember.Global
    public void ColorMarkedCells(
        Color color,
        Color? blankColor = null,
        bool flattenOthers = false,
        bool clear = false)
    {
        blankColor ??= _flatColor;
        ChangeCellColors(_markedCells, color, blankColor.Value, flattenOthers);
        if (clear) ClearMarkedCells();
    }

    

    #endregion
}*/