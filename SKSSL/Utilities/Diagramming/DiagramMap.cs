using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi;
using static SKSSL.Mathematics.IntegerExtensions;
using Point = Microsoft.Xna.Framework.Point;
using Vector2 = System.Numerics.Vector2;

namespace SKSSL.Utilities;

/// <summary>
/// Dynamic renderable cell map.
/// </summary>
public partial class DiagramMap
{
    // The Maps
    private readonly Texture2D _sourceMap; // Original diagram texture. Never modify it.
    private readonly Texture2D _displayMap; // Texture actually used for drawing.
    private readonly Texture2D? _overlay; // overlay with extras

    // Mono IO
    private readonly Texture2D _paletteTexture;
    private readonly GraphicsDevice _graphics;
    private readonly Effect _paletteEffect;

    // Original CPU-side pixels, retained separately from the display buffer.
    private readonly Color[] _displayPixels;
    private readonly Color[] _palettePixels;

    // Pixel ownership: pixel index -> current cell ID. -1 means no cell owns the pixel.
    private readonly int[] _pixelToCellId;

    // Reverse lookup: pixel index -> position inside its cell's list. -1 means the pixel isn't in a cell list.
    private readonly int[] _pixelIndexInCell;
    private readonly List<int>[] _pixelIndicesByCell; // Current pixel membership for each cell.
    private readonly Color[] _cellRawColors; // Immutable base colors and the ID lookup.
    private readonly Dictionary<uint, int> _paletteColorToId = new();

    private Rectangle _dirtyIdRectangle;
    private bool _idTextureDirty;
    private bool _paletteDirty;

    // Reused for partial rectangular uploads.
    private readonly Color[] _uploadScratch;
    private readonly int _paletteWidth;
    private readonly int _paletteHeight;

    // Alpha 128 is the empty-cell sentinel used by SetPixelCell.
    private static readonly Color EmptyCellIdColor = new(0, 0, 0, 0);

    // Options lifted from Voronoi Settings
    private readonly float _overlayOpacity;
    private readonly float _diagramOpacity;

    #region Constructors

    public DiagramMap(GraphicsDevice graphics, VoronoiDiagramData data)
    {
        // MonoGame Formalities
        _graphics = graphics;
        // OpenGL is cross-compat with the Big Three(TM) Operating Systems, so I am going with OpenGL.
        _paletteEffect = Shaders.EffectLoader.LoadEmbedded(graphics, "CellPalette_OpenGL.mgfxo");
        _overlayOpacity = data.VoronoiOptions.OverlayOpacity;
        _diagramOpacity = data.VoronoiOptions.DiagramOpacity;

        // Populate Source Map
        _sourceMap = new Texture2D(graphics,
            data.CellIdTexture.Width,
            data.CellIdTexture.Height,
            mipmap: false,
            format: SurfaceFormat.Color
        );
        _overlay = new Texture2D(graphics,
            _sourceMap.Width,
            _sourceMap.Height,
            mipmap: false,
            format: SurfaceFormat.Color
        );
        // Raw Overlay - Only if "None" is not toggled.
        DiagramOverlayOptions overlayFlags = data.VoronoiOptions.OverlayFlags;
        if (overlayFlags != DiagramOverlayOptions.SuppressAll)
            _overlay.SetData(data.OverlayColors);

        int pixelCount = _sourceMap.Width * _sourceMap.Height;
        var sourcePixels = new Color[pixelCount];
        _sourceMap.GetData(sourcePixels);

        // Populate Display Pixels using Source Map
        _displayPixels = new Color[pixelCount];
        _uploadScratch = new Color[pixelCount];

        // Populate Pixel Indexers
        _pixelToCellId = new int[pixelCount];
        _pixelIndexInCell = new int[pixelCount];
        Array.Copy(data.PixelToCellId, _pixelToCellId, pixelCount);
        Array.Fill(_pixelIndexInCell, -1);

        // Keep a Copy of the Original Per-Cell Colors
        int cellCount = data.Sites.Length;
        _cellRawColors = new Color[cellCount];
        Array.Copy(data.CellIdToCellColor, _cellRawColors, cellCount);

        // Pack Raw Colors for Cells
        _paletteColorToId.Clear();
        for (int id = 0; id < _cellRawColors.Length; id++)
        {
            uint packedColor = _cellRawColors[id].PackedValue;
            string e_0 = $"Duplicate palette color encountered for cell ID {id}.";
            if (!_paletteColorToId.TryAdd(packedColor, id)) throw new InvalidOperationException(e_0);
        }

        // Build Cell Overrides and do Indexing
        const string e_1 = "The RGB cell-ID encoding supports at most 16,777,216 cells.";
        if (cellCount > 0x1000000) throw new IndexOutOfRangeException(e_1);
        _cellOverrideColors = new Color[cellCount];
        _hasCellOverride = new bool[cellCount];
        _pixelIndicesByCell = new List<int>[cellCount];
        for (int i = 0; i < cellCount; i++) _pixelIndicesByCell[i] = [];
        BuildPixelIndexFromIds();

        // Build Rendering Cell Pixels
        _renderCellIdByPixel = new int[pixelCount];
        Array.Fill(_renderCellIdByPixel, -1);
        Array.Copy(_pixelToCellId, _renderCellIdByPixel, pixelCount);
        _renderParentCellId = new int[_cellRawColors.Length];
        for (int i = 0; i < _renderParentCellId.Length; i++)
            _renderParentCellId[i] = i;

        UpdateDisplayPixelsFromRenderIds(); // Keep after referenceBoundary. Updates routing.

        // Build the Palette
        _paletteWidth = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(cellCount)));
        _paletteHeight = Math.Max(1, (cellCount + _paletteWidth - 1) / _paletteWidth);
        _palettePixels = new Color[_paletteWidth * _paletteHeight];
        Array.Copy(_cellRawColors, _palettePixels, cellCount);

        // Finalize Display and Palette Textures
        //@formatter:off
        _sourceMap.SetData(sourcePixels);
        _displayMap = new Texture2D(graphics, _sourceMap.Width, _sourceMap.Height, mipmap: false, format: SurfaceFormat.Color);
        _paletteTexture = new Texture2D(graphics, _paletteWidth, _paletteHeight, mipmap: false, format: SurfaceFormat.Color);
        _displayMap.SetData(_displayPixels);
        _paletteTexture.SetData(_palettePixels);
        //@formatter:on

        // Create Highlighting
        //@formatter:off
        _highlightPixels = new Color[pixelCount];
        _cellHighlightColors = new Color[cellCount];
        _hasCellHighlight = new bool[cellCount];
        _highlightMap = new Texture2D(graphics, _sourceMap.Width, _sourceMap.Height, mipmap: false, format: SurfaceFormat.Color);
        Array.Fill(_highlightPixels, Color.Transparent);
        _highlightMap.SetData(_highlightPixels);
        //@formatter:on
    }

    #endregion

    public Texture2D GetRenderedMap() => _displayMap;
    public Texture2D? GetOverlay() => _overlay;

    /// Useful when one needs the untouched source.
    public Texture2D GetSourceMap() => _sourceMap;

    public void Draw(SpriteBatch? spriteBatch)
    {
        if (spriteBatch == null)
            return;

        FlushTextureUpdates();
        DrawDiagram(spriteBatch); // Main Diagram
        DrawOverlay(spriteBatch); // Overlay (Sites, etc.)
        DrawHighlights(spriteBatch); // Flashing Highlights
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawOverlay(SpriteBatch spriteBatch)
    {
        // The overlay is a normal image, not a cell-ID texture,
        // so render it separately without the palette shader.
        if (_overlay == null) return;
        spriteBatch.Begin(
            blendState: BlendState.AlphaBlend,
            sortMode: SpriteSortMode.Deferred,
            samplerState: SamplerState.PointClamp,
            transformMatrix: _diagramTransform
        );
        float opacity = MathHelper.Clamp(_overlayOpacity, 0f, 1f);
        spriteBatch.Draw(_overlay, Vector2.Zero, Color.White * opacity);
        spriteBatch.End();
    }

    // ReSharper disable once UnusedMember.Global
    public void Update(GameTime gameTime)
    {
        UpdateHighlights(gameTime);
    }

    /// <summary>
    /// Retrieve a cell at a provided mouse position.
    /// </summary>
    /// <param name="point">Usually Mouse Position.</param>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool TryGetCellAt(Point point, out uint id)
    {
        id = InvalidatedUint;

        // Convert screen coordinates into diagram-local coordinates.
        Matrix inverse = Matrix.Invert(_diagramTransform);
        var screenPosition = new Microsoft.Xna.Framework.Vector2(point.X, point.Y);
        Microsoft.Xna.Framework.Vector2 localPosition =
            Microsoft.Xna.Framework.Vector2.Transform(screenPosition, inverse);

        int x = (int)MathF.Floor(localPosition.X);
        int y = (int)MathF.Floor(localPosition.Y);
        if ((uint)x >= (uint)_sourceMap.Width || (uint)y >= (uint)_sourceMap.Height)
            return false;

        int pixelIndex = y * _sourceMap.Width + x;
        int cellId = _pixelToCellId[pixelIndex];

        if (cellId < 0)
            return false;

        id = (uint)cellId;
        return true;
    }
}