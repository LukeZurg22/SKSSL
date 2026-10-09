using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SKSSL.Utilities.Voronoi;
using Point = Microsoft.Xna.Framework.Point;
using Vector2 = System.Numerics.Vector2;

namespace SKSSL.Utilities;

/// <summary>
/// Dynamic renderable cell map.
/// </summary>
public partial class DiagramMap
{
    // The Maps
    private readonly Texture2D _pixelMap; // Original diagram texture. Never modify it.
    private readonly Texture2D _displayMap; // Texture actually used for drawing.
    private readonly Texture2D? _overlay;

    // Mono IO
    private readonly Texture2D _cellIdTexture;
    private readonly Texture2D _paletteTexture;
    private readonly GraphicsDevice _graphics;
    private readonly Effect _paletteEffect;

    // Original CPU-side pixels, retained separately from the display buffer.
    private readonly Color[] _sourcePixels;
    private readonly Color[] _displayPixels;
    private readonly Color[] _palettePixels;

    // Pixel ownership: pixel index -> current cell ID. -1 means no cell owns the pixel.
    private readonly int[] _cellIdByPixel;

    // Reverse lookup: pixel index -> position inside its cell's list. -1 means the pixel isn't in a cell list.
    private readonly int[] _pixelIndexInCell;
    private readonly List<int>[] _pixelIndicesByCell = []; // Current pixel membership for each cell.
    private Color[] _cellRawColors = []; // Immutable base colors and the ID lookup.

    private readonly PackedColorEntry[] _rawColorToIdLookup = [];
    private readonly int _rawColorToIdMask;

    // Override state, indexed by cell ID.
    private Color[] _cellOverrideColors = [];
    private bool[] _hasCellOverride = [];

    private Rectangle _dirtyIdRectangle;
    private bool _idTextureDirty;
    private bool _paletteDirty;

    // Reused for partial rectangular uploads.
    private readonly Color[] _uploadScratch;
    private readonly int _paletteWidth;
    private readonly int _paletteHeight;

    private const uint InvalidatedUint = uint.MaxValue;

    #region Constructors

    private DiagramMap(GraphicsDevice graphics, Texture2D pixelMap, Texture2D? overlay)
    {
        _pixelMap = pixelMap;
        _overlay = overlay;
        _graphics = graphics;

        // Read the source only once. Never read it back each frame.
        int pixelCount = pixelMap.Width * pixelMap.Height;
        _sourcePixels = new Color[pixelCount];
        _pixelMap.GetData(_sourcePixels);
        _displayPixels = new Color[pixelCount];
        _uploadScratch = new Color[pixelCount];
        _cellIdByPixel = new int[pixelCount];
        _pixelIndexInCell = new int[pixelCount];
        Array.Fill(_cellIdByPixel, -1);
        Array.Fill(_pixelIndexInCell, -1);

        // OpenGL is cross-compat with the Big Three(TM) Operating Systems, so I am going with OpenGL.
        _paletteEffect = Shaders.EffectLoader.LoadEmbedded(graphics, "CellPalette_OpenGL.mgfxo");
    }

    public DiagramMap(GraphicsDevice graphics, VoronoiDiagramData diagramData)
        : this(graphics, diagramData.PixelMap, diagramData.OverlayMap)
    {
        // Keep our own copy of the original per-cell colors.
        _cellRawColors = (Color[])diagramData.CellRawColors.Clone();
        _rawColorToIdLookup = diagramData.ColorToIdLookup;
        _rawColorToIdMask = diagramData.ColorToIdMask;

        // Building cell overrides and indexing.
        int cellCount = _cellRawColors.Length;
        if (cellCount > 0x1000000)
            throw new IndexOutOfRangeException("The RGB cell-ID encoding supports at most 16,777,216 cells.");
        _cellOverrideColors = new Color[cellCount];
        _hasCellOverride = new bool[cellCount];
        _pixelIndicesByCell = new List<int>[cellCount];
        for (int i = 0; i < cellCount; i++)
            _pixelIndicesByCell[i] = [];

        // Build the palette dimensions.
        //@formatter:off
        _paletteWidth = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(cellCount)));
        _paletteHeight = Math.Max(1, (cellCount + _paletteWidth - 1) / _paletteWidth);
        _palettePixels = new Color[_paletteWidth * _paletteHeight];
        Array.Copy(_cellRawColors, _palettePixels, cellCount);
        _displayMap = new Texture2D(graphics, _pixelMap.Width, _pixelMap.Height, mipmap: false, format: SurfaceFormat.Color);
        _cellIdTexture = new Texture2D(graphics, _pixelMap.Width, _pixelMap.Height, mipmap: false, format: SurfaceFormat.Color);
        _paletteTexture = new Texture2D(graphics, _paletteWidth, _paletteHeight, mipmap: false, format: SurfaceFormat.Color);
        //@formatter:on

        BuildPixelIndex();

        // The display buffer now contains the base cell colors.
        _cellIdTexture.SetData(_displayPixels);
        _paletteTexture.SetData(_palettePixels);
        _displayMap.SetData(_sourcePixels);
    }

    #endregion

    public Texture2D GetDiagram() => _displayMap;

    /// Useful when one needs the untouched source.
    public Texture2D GetOriginalDiagram() => _pixelMap;

    public void Draw(SpriteBatch? spriteBatch)
    {
        if (spriteBatch == null)
            return;

        FlushTextureUpdates();

        // Set all per-map shader parameters before drawing.
        _paletteEffect.Parameters["PaletteTexture"].SetValue(_paletteTexture);
        _paletteEffect.Parameters["BaseTexture"].SetValue(_pixelMap);
        _paletteEffect.Parameters["PaletteWidth"].SetValue((float)_paletteWidth);
        _paletteEffect.Parameters["PaletteHeight"].SetValue((float)_paletteHeight);

        // The cell-ID texture is bound by SpriteBatch.
        spriteBatch.Begin(
            sortMode: SpriteSortMode.Deferred,
            blendState: BlendState.AlphaBlend,
            samplerState: SamplerState.PointClamp,
            effect: _paletteEffect,
            transformMatrix: _diagramTransform);
        spriteBatch.Draw(_cellIdTexture, Vector2.Zero, Color.White);
        spriteBatch.End();

        // The overlay is a normal image, not a cell-ID texture,
        // so render it separately without the palette shader.
        if (_overlay != null)
        {
            spriteBatch.Begin(samplerState: SamplerState.PointClamp);
            spriteBatch.Draw(_overlay, Vector2.Zero, Color.White);
            spriteBatch.End();
        }
    }

    /// <summary>
    /// Retrieve a cell at a provided mouse position.
    /// </summary>
    /// <param name="mousePosition"></param>
    /// <param name="id"></param>
    /// <returns></returns>
    public bool TryGetCellAt(Point mousePosition, out uint id)
    {
        id = InvalidatedUint;

        // Convert screen coordinates into diagram-local coordinates.
        Matrix inverse = Matrix.Invert(_diagramTransform);
        var screenPosition = new Microsoft.Xna.Framework.Vector2(mousePosition.X, mousePosition.Y);
        Microsoft.Xna.Framework.Vector2 localPosition =
            Microsoft.Xna.Framework.Vector2.Transform(screenPosition, inverse);

        int x = (int)MathF.Floor(localPosition.X);
        int y = (int)MathF.Floor(localPosition.Y);
        if ((uint)x >= (uint)_pixelMap.Width || (uint)y >= (uint)_pixelMap.Height)
            return false;

        int pixelIndex = y * _pixelMap.Width + x;
        int cellId = _cellIdByPixel[pixelIndex];

        if (cellId < 0)
            return false;

        id = (uint)cellId;
        return true;
    }
}