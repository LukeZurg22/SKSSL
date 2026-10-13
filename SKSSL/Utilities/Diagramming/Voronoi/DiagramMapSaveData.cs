using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

    /// Serialize as a separate, user-friendly map that maps CELL ID -> CELL COLOR.
    public Dictionary<int, uint> CellIdToColor { get; set; } = new();

    #region I/O === Cell ID -> Color

    private const string FileNameCellsXML = "cells.xml";

    public void SaveCellIds(string path)
    {
        var contents = CellIdToColor
            .Select(entry => $"{entry.Key};{entry.Value.ToString(CultureInfo.InvariantCulture)}");
        File.WriteAllLines(Path.Combine(path, FileNameCellsXML), contents);
    }

    public void LoadCellIds(string path)
    {
        CellIdToColor.Clear();
        foreach (string line in File.ReadLines(path))
        {
            string[] parts = line.Split(';');
            if (parts.Length != 2)
                continue;

            bool canKey = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int key);
            bool canValue = uint.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out uint value);
            if (canKey && canValue) CellIdToColor[key] = value;
        }
    }

    #endregion

    // WIP: Reconstruct this in LOAD.
    //public int[] PixelToCellId { get; set; }


    // WIP: Note fields from DiagramMap To-> here. Map IMAGE data may need to be serialized to a binary file,
    //  ...perhaps even compressed?

    /// <summary>
    /// Save a <see cref="DiagramMap"/> for later use at a provided location, with a dedicated name.
    /// </summary>
    /// <param name="diagramName">Name of the diagram.</param>
    /// <param name="hostFolderPath">The path where the diagram will be saved in its own directory.</param>
    /// <remarks>
    /// [MyGameFolder]<br/>
    /// -> WorldFolder<br/>
    /// --> WorldFolder/MyDiagram<br/>
    /// +--> WorldFolder/MyDiagram (a bunch of data)
    /// </remarks>
    public void SaveDiagram(string diagramName, string hostFolderPath)
    {
        // Create a directory with the diagram name.
        DirectoryInfo diagramDirectory = Directory.CreateDirectory(Path.Combine(hostFolderPath, diagramName));

        // TODO: Serialize everything else.
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

        // Load Cell Id -> Cell Color Map
        /* ===Diagram===
         * -> [DiagramDirectory]
         * +--> DiagramDirectory/cells.xml */
        LoadCellIds(Path.Combine(diagramDirectory, FileNameCellsXML));

        // Load PNG Texture of Raw Cell Data & Map to CellIdToCellColor

        // Load (nullable) Overlay Colors

        // Load Voronoi Options (VoronoiOptions only contains simple types! It'll be fine!)

        // TODO: Populate this.
        var dd = new VoronoiDiagramData([],
            [],
            [],
            null,
            null,
            null);
        return dd;
    }
}