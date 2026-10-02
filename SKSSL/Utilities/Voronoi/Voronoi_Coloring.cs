using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private readonly HashSet<uint> _markedCells = []; // Collected cells to interact with.
    private uint[] _cellIdBySiteId = [];

    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private Texture2D? _highlightMask; // Mask for highlighting cells.
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
        if (_cellsById.ContainsKey(cell))
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

    /// <summary>
    /// Sets all cells to a single color, which defaults to a provided/default flat color.
    /// </summary>
    // ReSharper disable once UnusedMember.Global
    public void ChangeAllCellColors(Color? color = null)
    {
        if (!_isGenerated)
            return;

        color ??= _flatColor;
        BuildUniformCellBatch();
        UpdateTexture(_previousBoundaryMode, _previousFlags, _previousThickness, _previousPointSize, true);
        return;

        void BuildUniformCellBatch()
        {
            int count = _cellGeometry.Length;
            if (_cellBatchVertices.Length < count)
                _cellBatchVertices = new VertexPositionColor[count];

            for (int i = 0; i < count; i++)
                _cellBatchVertices[i] = new VertexPositionColor(_cellGeometry[i], color.Value);

            _cellBatchPrimitiveCount = count / 3;
        }
    }

    /// <summary>
    /// Change multiple cells to a set color.
    /// </summary>
    /// <param name="idsAffected"></param>
    /// <param name="color"></param>
    /// <param name="blankColor"></param>
    /// <param name="flattenOthers"></param>
    /// <remarks>Make sure to call <see cref="UpdateTexture"/> afterwards.</remarks>
    private void ChangeCellColors(HashSet<uint> idsAffected, Color color, Color blankColor, bool flattenOthers)
    {
        if (!_isGenerated)
            return;

        // Rebuild french cell batch with colors provided. Internal logic will handle the way they are colored,
        //  and outside of this function they are handled as if no ids were affected.
        BuildCellColorBatch(idsAffected, flattenOthers, (color, blankColor));
    }


    /// <summary>
    /// Change color for a specific cell ID.
    /// </summary>
    /// <param name="cellId"></param>
    /// <param name="color"></param>
    /// <remarks>Make sure to call <see cref="UpdateTexture"/> afterwards.</remarks>
    public unsafe void ChangeCellColor(uint cellId, Color color)
    {
        if (!_isGenerated)
            return;

        // Rebuild cell batch with colors provided. Internal logic will handle the way they are colored,
        //  and outside of this function they are handled as if no ids were affected.
        // TODO: Currently sets Vertex colors directly. May be optimized by GPU instance adjustment.
        if (cellId >= (uint)_renderingCells.Length)
            return;

        int index = (int)cellId;
        int count = _cellGeometryCounts[index];

        if (count <= 0)
            return;

        int start = _cellGeometryOffsets[index];
        fixed (VertexPositionColor* outputPtr = _cellBatchVertices)
        {
            var dst = outputPtr + start;
            for (int i = 0; i < count; i++)
                dst[i].Color = color;
        }
    }

    #endregion

    #region Demarcating Cells by ID

    private readonly HashSet<uint> _demarcatedCellIds = [];

    /// <summary>
    /// Utilize the cell marking system to demarcate cells, rather than reconstruct a separate demarcation lists.
    /// Calls <see cref="DemarcateCells"/>.
    /// </summary>
    /// <param name="edgeColor"></param>
    /// <param name="thickness"></param>
    /// <param name="rebuildBatch"></param>
    /// <param name="clearMarks"></param>
    // ReSharper disable once UnusedMember.Global
    public void DemarcateMarkedCells(
        Color edgeColor,
        float thickness = 1f,
        bool rebuildBatch = false,
        bool clearMarks = false)
    {
        DemarcateCells(_markedCells, edgeColor, thickness, rebuildBatch);
        if (clearMarks) ClearMarkedCells();
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void DemarcateCells(
        IEnumerable<uint> cells,
        Color edgeColor,
        float thickness = 1f,
        bool rebuildBatch = false)
    {
        ArgumentNullException.ThrowIfNull(cells);

        _demarcatedCellIds.Clear();
        foreach (var cell in cells)
            DemarcateCell(cell, thickness);

        if (rebuildBatch) RebuildDemarcatedCellBorders(edgeColor, thickness);
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public void DemarcateCell(uint cellId, float thickness = 1f)
    {
        if (!_cellsById.TryGetValue(cellId, out VoronoiCell? _))
            return;

        _demarcatedCellIds.Add(cellId);
    }

    // ReSharper disable once UnusedMember.Global
    public void ClearDemarcatedCells()
    {
        _demarcatedCellIds.Clear();
        _demarcateBatchVertices = [];
        _demarcateBatchPrimitiveCount = 0;
    }

    #endregion

    #region Demarcating Cells by Color (Avoid Using These!)

    [UsedImplicitly, Obsolete("It may be best to highlight cells by ID, rather than color.")]
    public void SetColorToDemarcate(Color color, float thickness = 1f, bool rebuild = true)
    {
        _demarcatedCellIds.Clear();
        for (int i = 0; i < _renderingCells.Length; i++)
            if (_cellRawColors[i].PackedValue == color.PackedValue)
                _demarcatedCellIds.Add(_renderingCells[i].ID);

        if (rebuild) RebuildDemarcatedCellBorders(color, thickness);
    }


    [UsedImplicitly, Obsolete("It may be best to highlight cells by ID, rather than color.")]
    public void SetColorsToDemarcate(IEnumerable<Color> colors, Color highlightColor, float thickness = 1f)
    {
        ArgumentNullException.ThrowIfNull(colors);

        foreach (Color color in colors)
        {
            SetColorToDemarcate(color, thickness, false);
        }

        RebuildDemarcatedCellBorders(highlightColor, thickness);
    }

    #endregion

    #region Utility

    public void RebuildDemarcatedCellBorders(Color color, float thickness)
    {
        if (_demarcatedCellIds.Count == 0 || _cellVoronoiEdges.Count == 0)
        {
            ClearDemarcatedCells();
            return;
        }

        /*
         * Every Voronoi edge is considered exactly once.
         * An edge is highlighted when exactly one of its two cells is selected.
         *
         * Therefore:
         *     selected <-> selected   = internal edge, skip
         *     selected <-> unselected = boundary, draw
         *     selected <-> outside    = boundary, draw
         */
        var vertices = new List<VertexPositionColor>(_demarcatedCellIds.Count * 18);

        foreach (CellVoronoiEdge edge in _cellVoronoiEdges)
        {
            uint cellA = GetCellIdFromSiteId(edge.SiteA.ID);
            bool aHighlighted = cellA != VoronoiCell.InvalidId && _demarcatedCellIds.Contains(cellA);
            bool bHighlighted = false;

            if (edge.SiteB.HasValue)
            {
                uint cellB = GetCellIdFromSiteId(edge.SiteB.Value.ID);
                bHighlighted = cellB != VoronoiCell.InvalidId && _demarcatedCellIds.Contains(cellB);
            }

            if (aHighlighted == bHighlighted)
                continue;

            AddDemarcateEdge(ref vertices, edge.Point1, edge.Point2, color, thickness);
        }

        _demarcateBatchVertices = vertices.ToArray();
        _demarcateBatchPrimitiveCount = _demarcateBatchVertices.Length / 3;
    }

    public void ForceUpdate()
    {
        if (!_isGenerated)
            return;

        UpdateTexture(_previousBoundaryMode, _previousFlags, _previousThickness, _previousPointSize, true);
    }

    // ReSharper disable once UnusedMember.Global
    public Color GetCellColor(uint cellId) => _cellRawColors[cellId];

    private Color GetCellColor(VoronoiCell cell)
    {
        Color color;
        uint hash;
        byte r, g, b;
        switch (CellDrawMode)
        {
            case ColorMode.Deterministic_Lines:
                hash = (uint)cell.Site.X + (uint)cell.Site.Y;
                hash ^= hash >> 16;
                r = (byte)hash;
                hash ^= hash >> 15;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                return color;
            case ColorMode.Deterministic_Random: // Throw together a lazy hash based on cell Site vertex.
                hash = (uint)cell.Site.X ^ (uint)cell.Site.Y;
                hash ^= hash >> 16;
                hash *= 0x7FEB352Du;
                r = (byte)hash;
                hash ^= hash >> 15;
                hash *= 0x846CA68Bu;
                g = (byte)hash;
                hash ^= hash >> 16;
                b = (byte)hash;
                color = new Color((int)r, g, b, 255);
                return color;
            case ColorMode.Random: // Truly random 0 -> 255
                color = new Color(
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    _random.Next(0, 256),
                    255);
                return color;
            case ColorMode.Semi_Deterministic_Unique:
                // Grab a deterministic semi-unique color and go an integer check.
                // Naturally if it isn't unique, then it is reaching the birthday-paradox point
                color = ColorUtilities.GetSemiUniqueColor(cell.Site.ID);
                int reversed = (color.R << 16) | (color.G << 8) | color.B;
                lock (_usedColors)
                {
                    // If the semi-unique color turns out to no longer be unique, then
                    // defaulting to the thread-dangerous unique color generator is the
                    // next best option. I am aware this causes an inner-dependency, and
                    // may also cause a little overhead. The deterministic method is faster
                    // than relying on the Random class to do its calls, and that for maps
                    // approximately smaller than 2000x2000, this would be incredibly efficient.
                    // As far as I see it, it's a small, but nevertheless preferred -optimization.
                    if (!_usedColors.Add(reversed)) goto case ColorMode.Unique;
                }

                return color;
            case ColorMode.Unique: // Pure random RGB – three integer ops, no floats
                int rgb;
                lock (_random)
                lock (_usedColors)
                {
                    do rgb = _random.Next(0x1000000); // 0 … 16 777 215
                    while (!_usedColors.Add(rgb));
                }

                color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF, 220);
                break;
            case ColorMode.Unified:
            default:
                color = _edgeColor;
                break;
        }

        return color;
    }

    #endregion
}