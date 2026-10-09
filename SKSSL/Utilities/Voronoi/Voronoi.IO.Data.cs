using System;
using System.Collections.Generic;
using System.IO;
using Clipper2Lib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

public partial class Voronoi
{
    private const uint SaveMagic = 0x4F524F56; // "VORO" in little-endian storage
    private const int MaxSerializedItems = 50_000_000;

    /// <summary>
    /// Writes the generated diagram to a versioned binary snapshot.
    /// </summary>
    public void SaveDiagram(string filePath)
    {
        if (!_isGenerated)
            throw new InvalidOperationException("Generate a diagram before saving it.");

        string fullPath = Path.GetFullPath(filePath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        // Write to a temporary file so an incomplete save does not leave
        // a partially written snapshot at the target path.
        string temporaryPath = fullPath + ".tmp";

        try
        {
            using (var stream = new FileStream(
                       temporaryPath,
                       FileMode.Create,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 1 << 20,
                       options: FileOptions.SequentialScan))
            using (var writer = new BinaryWriter(stream))
            {
                // Header and format information.
                writer.Write(SaveMagic);
                writer.Write(Version);

                writer.Write(_width);
                writer.Write(_height);

                writer.Write((int)CellDrawMode);
                WriteColor(writer, _edgeColor);
                WriteColor(writer, _pointColor);
                WriteColor(writer, _flatColor);

                writer.Write((int)_boundaryMode);
                writer.Write(_firstArtificialId);

                // Last-used rendering configuration.
                writer.Write((int)_previousFlags);
                writer.Write((int)_previousBoundaryMode);
                writer.Write(_previousThickness);
                writer.Write(_previousPointSize);

                // Original point/site array. IDs are persisted verbatim.
                writer.Write(_points.Length);

                foreach (Point point in _points)
                    WritePoint(writer, point);

                // Save the full ID-indexed cell array, including null slots
                // and culled cells. Do not save only _renderingCells.
                writer.Write(_cellsBySiteId.Length);

                foreach (VoronoiCell? cell in _cellsBySiteId)
                {
                    writer.Write(cell is not null);

                    if (cell is not null)
                        WriteCell(writer, cell);
                }

                // Preserve colors indexed by ID.
                WriteColors(writer, _cellRawColors);
                WriteColors(writer, _cellOverrideColors);

                // Preserve render ordering using cell-array slots, not IDs.
                // InvalidID cells can share the same ID, so IDs cannot uniquely identify a cell.
                var cellSlots = new Dictionary<VoronoiCell, int>(ReferenceEqualityComparer.Instance);
                for (int i = 0; i < _cellsBySiteId.Length; i++)
                {
                    VoronoiCell? cell = _cellsBySiteId[i];
                    if (cell is not null) cellSlots.TryAdd(cell, i);
                }

                writer.Write(_renderingCells.Length);

                foreach (VoronoiCell cell in _renderingCells)
                {
                    if (!cellSlots.TryGetValue(cell, out int slot))
                    {
                        string e = $"Rendering cell with ID {cell.ID} is not present in _cellsBySiteId.";
                        throw new InvalidOperationException(e);
                    }

                    writer.Write(slot);
                }
            }

            // Replace the target only after the complete file is written.
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            throw;
        }
    }

    /// <summary>
    /// Loads a saved diagram into a new Voronoi instance.
    /// Rendering resources are created for the supplied GraphicsDevice.
    /// </summary>
    public static Voronoi LoadDiagram(GraphicsDevice graphicsDevice, string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1 << 20,
            options: FileOptions.SequentialScan);

        using var reader = new BinaryReader(stream);

        if (reader.ReadUInt32() != SaveMagic)
            throw new InvalidDataException("The file is not a Voronoi diagram snapshot.");

        int formatVersion = reader.ReadInt32();
        if (formatVersion != Version)
            Log(new InvalidDataException($"Unsupported Voronoi save version: {formatVersion}."));

        int width = reader.ReadInt32();
        int height = reader.ReadInt32();

        if (width <= 0 || height <= 0)
            throw new InvalidDataException("The saved diagram dimensions are invalid.");

        var cellDrawMode = (ColorMode)reader.ReadInt32();
        Color edgeColor = ReadColor(reader);
        Color pointColor = ReadColor(reader);
        Color flatColor = ReadColor(reader);

        var boundaryMode = (VoronoiBoundaryMode)reader.ReadInt32();
        uint firstArtificialId = reader.ReadUInt32();

        var flags = (VoronoiRenderingFlags)reader.ReadInt32();
        var previousBoundaryMode = (VoronoiBoundaryMode)reader.ReadInt32();

        float thickness = reader.ReadSingle();
        float pointSize = reader.ReadSingle();

        // Read persistent data into ordinary CPU-side structures first.
        var points = ReadPoints(reader);
        var cells = ReadCells(reader);
        var rawColors = ReadColors(reader);
        var overrideColors = ReadColors(reader);

        int renderCount = ReadCount(reader);
        var renderingSlots = new int[renderCount];

        for (int i = 0; i < renderCount; i++)
            renderingSlots[i] = reader.ReadInt32();

        // Detect truncated or unexpected trailing data.
        if (stream.Position != stream.Length)
            throw new InvalidDataException(
                "The saved diagram contains unexpected trailing data.");

        var diagram = new Voronoi(graphicsDevice, cellDrawMode, edgeColor, pointColor, flatColor)
        {
            _width = width,
            _height = height,
            _boundaryMode = boundaryMode,
            _firstArtificialId = firstArtificialId,
            _points = points,
            _cellsBySiteId = cells,
            _previousFlags = flags,
            _previousBoundaryMode = previousBoundaryMode,
            _previousThickness = thickness,
            _previousPointSize = pointSize
        };

        // The constructor initially used the current viewport dimensions.
        diagram.SetDiagramProjection(Matrix.Identity, Matrix.Identity);
        diagram.RebuildAfterLoad(renderingSlots, rawColors, overrideColors);
        return diagram;
    }

    private void RebuildAfterLoad(int[] renderingSlots, Color[] rawColors, Color[] overrideColors)
    {
        // Rebuild derived edge information from the saved sites.
        // The saved cell polygons themselves are NOT regenerated.
        _voronoiEdges.Clear();
        _cellVoronoiEdges.Clear();
        _edgesByCell = new List<CellVoronoiEdge>?[_cellsBySiteId.Length];

        using var delaunay = new DelaunayTriangulator();

        // Initialize the triangulation's bounds for the saved diagram.
        // Discard the generated points; the saved points remain authoritative.
        _ = delaunay.CreatePointsList(_width, _height, (uint)_points.Length);
        var triangulation = delaunay.BowyerWatson(_points);

        foreach (Triangle triangle in triangulation)
            AddVoronoiEdges(triangle, _boundaryMode);

        // Rebuild internal render mappings from the restored cells.
        BuildRenderArrays();

        // Build an explicit ID lookup to restore the original render order.
        // Restore rendering order by exact cell-array references.
        // This works even when multiple cells have InvalidID.
        _renderingCells = new VoronoiCell[renderingSlots.Length];

        for (int i = 0; i < renderingSlots.Length; i++)
        {
            int slot = renderingSlots[i];
            if ((uint)slot >= (uint)_cellsBySiteId.Length)
            {
                throw new InvalidDataException($"Invalid rendering cell slot: {slot}.");
            }

            VoronoiCell? cell = _cellsBySiteId[slot];

            var message = $"Rendering cell slot {slot} references an empty cell.";
            _renderingCells[i] = cell ?? throw new InvalidDataException(message);
        }

        // BuildRenderArrays may have created fresh colors.
        // Restore the exact arrays saved with the diagram.
        _cellRawColors = rawColors;
        _cellOverrideColors = overrideColors;

        _usedColors.Clear();
        foreach (Color color in _cellRawColors)
            _usedColors.Add((color.R << 16) | (color.G << 8) | color.B);

        // Geometry, tessellations and rendering batches are derived data.
        BuildCellGeometry();
        BuildDemarcateCellEdges();

        if ((_previousFlags & VoronoiRenderingFlags.Points) != 0)
            BuildPointBatch(_previousPointSize);

        if ((_previousFlags & VoronoiRenderingFlags.Edges) != 0)
            BuildEdgeBatch(_previousThickness);

        if ((_previousFlags & VoronoiRenderingFlags.Triangles) != 0)
            BuildTriangleBatch(triangulation);

        if ((_previousFlags & VoronoiRenderingFlags.Cells) != 0)
        {
            BuildCellVertexBatch();
            BuildCellColorBatch([]);
        }

        _isGenerated = true;
        _textureValid = false;

        UpdateTexture(_boundaryMode, _previousFlags, _previousThickness, _previousPointSize);
    }

    private static void WriteCell(BinaryWriter writer, VoronoiCell cell)
    {
        WritePoint(writer, cell.Site);

        writer.Write(cell.IsBoundary);
        writer.Write(cell.IsCulled);
        writer.Write(cell.Vertices.Count);

        foreach (Point vertex in cell.Vertices)
            WritePoint(writer, vertex);

        // Clipper2 uses integer coordinates here. Store its longs directly
        // instead of converting them into floats.
        writer.Write(cell.RenderPaths is not null);
        if (cell.RenderPaths is null)
            return;

        writer.Write(cell.RenderPaths.Count);

        foreach (Path64 path in cell.RenderPaths)
        {
            writer.Write(path.Count);
            foreach (Point64 point in path)
            {
                writer.Write(point.X);
                writer.Write(point.Y);
            }
        }
    }

    private static VoronoiCell ReadCell(BinaryReader reader)
    {
        Point site = ReadPoint(reader);

        bool isBoundary = reader.ReadBoolean();
        bool isCulled = reader.ReadBoolean();

        int vertexCount = ReadCount(reader);
        var vertices = new List<Point>(vertexCount);

        for (int i = 0; i < vertexCount; i++)
            vertices.Add(ReadPoint(reader));

        var cell = new VoronoiCell(site, vertices)
        {
            IsBoundary = isBoundary,
            IsCulled = isCulled
        };

        if (reader.ReadBoolean())
        {
            int pathCount = ReadCount(reader);
            var paths = new Paths64();

            for (int i = 0; i < pathCount; i++)
            {
                int pointCount = ReadCount(reader);
                var path = new Path64();

                for (int j = 0; j < pointCount; j++)
                {
                    long x = reader.ReadInt64();
                    long y = reader.ReadInt64();

                    path.Add(new Point64(x, y));
                }

                paths.Add(path);
            }

            cell.RenderPaths = paths;
            return cell;
        }

        cell.RenderPaths = null;
        return cell;
    }

    private static Point[] ReadPoints(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var points = new Point[count];

        for (int i = 0; i < count; i++)
            points[i] = ReadPoint(reader);

        return points;
    }

    private static void WritePoint(BinaryWriter writer, Point point)
    {
        writer.Write(point.X);
        writer.Write(point.Y);
        writer.Write(point.ID);
    }

    private static Point ReadPoint(BinaryReader reader)
    {
        float x = reader.ReadSingle();
        float y = reader.ReadSingle();
        uint id = reader.ReadUInt32();
        return new Point(x, y, id);
    }

    private static VoronoiCell?[] ReadCells(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var cells = new VoronoiCell?[count];
        for (int i = 0; i < count; i++)
            if (reader.ReadBoolean())
                cells[i] = ReadCell(reader);

        return cells;
    }

    private static void WriteColors(
        BinaryWriter writer,
        Color[] colors)
    {
        writer.Write(colors.Length);

        foreach (Color color in colors)
            WriteColor(writer, color);
    }

    private static Color[] ReadColors(BinaryReader reader)
    {
        int count = ReadCount(reader);
        var colors = new Color[count];

        for (int i = 0; i < count; i++)
            colors[i] = ReadColor(reader);

        return colors;
    }

    private static void WriteColor(
        BinaryWriter writer,
        Color color)
    {
        writer.Write(color.R);
        writer.Write(color.G);
        writer.Write(color.B);
        writer.Write(color.A);
    }

    private static Color ReadColor(BinaryReader reader)
    {
        byte r = reader.ReadByte();
        byte g = reader.ReadByte();
        byte b = reader.ReadByte();
        byte a = reader.ReadByte();

        return new Color(r, g, b, a);
    }

    private static int ReadCount(BinaryReader reader)
    {
        int count = reader.ReadInt32();

        if (count < 0 || count > MaxSerializedItems)
            throw new InvalidDataException(
                $"Invalid item count in Voronoi snapshot: {count}.");

        return count;
    }
}