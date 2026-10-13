using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

/// Reconstruction data to allow a diagram to be reconstructed.
public class DiagramMapSaveData
{
    // WIP: Save as YAML
    public VoronoiOptions VoronoiOptions { get; set; }

    public int Width { get; set; } // Width of Image (not window)
    public int Height { get; set; } // Height of Image (not window)

    // WIP: Who cares about the source mode? public VoronoiSourceMode SourceMode { get; set; }
    public VoronoiBoundaryMode BoundaryMode { get; set; }

    public Vector2[] Sites { get; set; } = [];
    public Color[] CellIdToCellColor { get; set; } = [];
    public Color[]? OverlayColors { get; set; } = [];

    // WIP: Serialize the full raster as a .png to read.
    /// The most "expensive" data that needs binary serialization.
    public uint[] RawPixelCellData { get; set; } = [];

    #region I/O === Cell ID -> Color

    private const string FileNameCellsXML = "cells.xml";

    /// Serialize as a separate, user-friendly map that maps CELL ID -> CELL COLOR.
    public static void SaveCellIds(string path)
    {
        //var contents = CellIdToColor
        //    .Select(entry => $"{entry.Key};{entry.Value.ToString(CultureInfo.InvariantCulture)}");
        //File.WriteAllLines(Path.Combine(path, FileNameCellsXML), contents);
    }

    /// <summary>
    /// Loads cell ids in parallel from a file.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cellCount"></param>
    private static Color[] LoadCellIdsToColors(string path, int cellCount)
    {
        var cellColors = new Color[cellCount];
        string[] lines = File.ReadAllLines(path);
        var entries = lines
            .AsParallel()
            .Select(line =>
            {
                // Ignore any lines starting with a comment, or empty lines.
                var text = line.AsSpan().Trim();
                if (text.IsEmpty || text[0] == '#') return null;

                // If a line is incomplete, short-circuit.
                string[] parts = line.Split(';');
                if (parts.Length < 2) return null;

                // Remove comments, and short-circuit if the comment -was- the second half.
                string cleanedLine = line.Split('#')[0].Trim();
                if (cleanedLine.Length == 0) return null;
                parts[1] = cleanedLine;

                // Parse Key and Value for int/uint KVP, where uint is a packed color.
                bool ky = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key);
                bool va = uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value);
                return !ky || !va ? ((int Key, uint Value)?)null : (key, value);
            })
            .Where(entry => entry.HasValue)
            .Select(entry => entry!.Value)
            .ToArray();

        foreach ((int Key, uint Value) entry in entries)
            cellColors[entry.Key] = new Color(entry.Value);

        return cellColors;
    }

    #endregion

    // WIP: Reconstruct this in LOAD.
    //public int[] PixelToCellId { get; set; }


    // WIP: Note fields from DiagramMap To-> here. Map IMAGE data may need to be serialized to a binary file,
    //  ...perhaps even compressed?

    /// <summary>
    /// Save a <see cref="DiagramMap"/> for later use at a provided location, with a dedicated name.
    /// </summary>
    /// <param name="diagramMap">Used to get Diagram Data to save</param>
    /// <param name="hostFolderPath">The path where the diagram will be saved in its own directory.</param>
    /// <remarks>
    /// [MyGameFolder]<br/>
    /// -> WorldFolder<br/>
    /// --> WorldFolder/MyDiagram<br/>
    /// +--> WorldFolder/MyDiagram (a bunch of data)
    /// </remarks>
    public void SaveDiagram(DiagramMap diagramMap, string hostFolderPath)
    {
        // Create a directory with the diagram name.
        DirectoryInfo diagramDirectory =
            Directory.CreateDirectory(Path.Combine(hostFolderPath, diagramMap.DiagramName));

        // TODO: Serialize everything else.

        // TODO: Assign CellIds dictionary.
        SaveCellIds(diagramDirectory.ToString());
    }

    /// <summary>
    /// Load a diagram from disk into memory; restoring its state.
    /// </summary>
    /// <param name="graphics">MonoGame graphics device, for rendering.</param>
    /// <param name="diagramDirectory">The path where the diagram data will be saved.</param>
    /// <returns></returns>
    /// <remarks>
    /// The objective is to perfectly recreate the conditions that allowed a <see cref="DiagramMap"/> to be
    /// constructed in the first place. Odd fields / properties need to be manually assigned after the fact;
    /// preferrably in a separate constructor in that class that calls "<b>:this(etc.)</b>"
    /// </remarks>
    public DiagramMap LoadDiagram(GraphicsDevice graphics, string diagramDirectory)
    {
        VoronoiDiagramData diagramData = LoadDiagramData(diagramDirectory);
        return new DiagramMap(graphics, diagramData);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private VoronoiDiagramData LoadDiagramData(string diagramDirectory)
    {
        // Load Width & Height
        // TEMP: Pixel count can be reconstructed from these two.

        // Load Sites
        int cellCount = 0;

        // Load Cell Id -> Cell Color Map
        /* ===Diagram===
         * -> [DiagramDirectory]
         * +--> DiagramDirectory/cells.xml */
        var cellIdToCellColor = LoadCellIdsToColors(Path.Combine(diagramDirectory, FileNameCellsXML), cellCount);

        // Load PNG Texture of Raw Cell Data & Map to CellIdToCellColor

        // Load (nullable) Overlay Colors

        // Load Voronoi Options (VoronoiOptions only contains simple types! It'll be fine!)

        // TODO: Populate this.
        var dd = new VoronoiDiagramData(
            [],
            cellIdToCellColor,
            [],
            null,
            null,
            null);
        return dd;
    }
}