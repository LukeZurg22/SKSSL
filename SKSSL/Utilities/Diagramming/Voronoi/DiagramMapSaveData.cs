using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace SKSSL.Utilities.Voronoi;

/// Reconstruction data to allow a diagram to be reconstructed.
public class DiagramMapSaveData
{
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
        
        // WIP: Sites
        // WIP: Height & Width
        // WIP: Serialize the full raster as a .png to read.
        //  The most "expensive" data that needs binary serialization. :: uint[] RawPixelCellData
        // WIP: Raw Cell Texture and Overlay Texture 
        // WIP: Who cares about the source mode? "VoronoiSourceMode SourceMode { get; set; }"
        // WIP: Save VoronoiOptions as YAML
        // WIP: Save Boundary Mode
        // WIP: Save "Color[] CellIdToCellColor"
        // WIP: Save "Color[]? OverlayColors"
        
        // WIP: Note fields from DiagramMap To-> here. Map IMAGE data may need to be serialized to a binary file,
        //  ...perhaps even compressed?

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
    public static DiagramMap LoadDiagram(GraphicsDevice graphics, string diagramDirectory) 
        => new(graphics, ReconstructDiagramData(diagramDirectory));

    private static VoronoiDiagramData ReconstructDiagramData(string diagramDirectory)
    {
        // Load Diagram Name
        string diagramName = Path.GetFileName(diagramDirectory);

        // Load PNG Texture of Raw Cell Data for Width, Height, and Pixel Count
        // TEMP: Pixel count can be reconstructed from these two.
        int width = 0;
        int height = 0;
        int pixelCount = width * height;
        Texture2D rawPixelMap = null;
        Color[] overlayColors = [];

        // Load Sites
        // TODO: Construct Sites
        Vector2[] sites = [];
        int cellCount = 0;

        // Load Cell Id -> Cell Color Map
        /* ===Diagram===
         * -> [DiagramDirectory]
         * +--> DiagramDirectory/cells.xml */
        var rawColorData = ReconstructCellIdsToColors(Path.Combine(diagramDirectory, FileNameCellsXML), cellCount);

        // Map to CellIdToCellColor
        // TODO: Construct raw cell texture.
        // TODO: Construct pixel -> cell ID
        int[] pixelToCellId = ReconstructPixelsToCellIds(rawColorData, pixelCount, rawPixelMap);

        // Load (nullable) Overlay Colors

        // Load Voronoi Options (VoronoiOptions only contains simple types! It'll be fine!)
        // TODO: Construct Voronoi Options from a serialized file.
        var voronoiOptions = new VoronoiOptions
        {
            DiagramName = diagramName
        };

        // TODO: Populate this.
        var diagramData = new VoronoiDiagramData(
            sites,
            rawColorData.CellColors,
            pixelToCellId,
            rawPixelMap,
            overlayColors,
            voronoiOptions);
        return diagramData;
    }

    #region I/O === Cell ID -> Color

    private const string FileNameCellsXML = "cells.xml";

    /// Serialize as a separate, user-friendly map that maps CELL ID -> CELL COLOR.
    public static void SaveCellIds(string path)
    {
        // WIP: This needs a Get method or something from the original Diagram.
        //var contents = CellIdToColor
        //    .Select(entry => $"{entry.Key};{entry.Value.ToString(CultureInfo.InvariantCulture)}");
        //File.WriteAllLines(Path.Combine(path, FileNameCellsXML), contents);
    }

    /// <summary>
    /// Loads cell ids in parallel from a file.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="cellCount"></param>
    private static (Color[] CellColors, Dictionary<uint, int> PaletteColorToId) ReconstructCellIdsToColors(
        string path,
        int cellCount)
    {
        var cellColors = new Color[cellCount];
        var paletteColorToId = new Dictionary<uint, int>(cellCount);
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

        paletteColorToId.Clear();
        foreach ((int Key, uint Value) entry in entries)
        {
            cellColors[entry.Key] = new Color(entry.Value);
            string e_0 = $"Duplicate palette color encountered for cell ID {entry.Key}.";
            if (!paletteColorToId.TryAdd(entry.Value, entry.Key)) throw new InvalidOperationException(e_0);
        }

        return (cellColors, paletteColorToId);
    }

    #endregion

    private static int[] ReconstructPixelsToCellIds(
        (Color[] CellColors, Dictionary<uint, int> PaletteColorToId) rawCellData,
        int pixelCount,
        Texture2D rawPixelMap)
    {
        int[] pixelsToCellId = new int[pixelCount];
        var texturePixelData = new uint[pixelCount];
        rawPixelMap.GetData(texturePixelData);
        foreach (var pixel in texturePixelData)
        {
            var id = rawCellData.PaletteColorToId[pixel];
            pixelsToCellId[pixel] = id;
        }

        return pixelsToCellId;
    }
}