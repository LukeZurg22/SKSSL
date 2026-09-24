using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static System.Math;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

public unsafe class DistributorImage : IPointDistributor
{
    // Constant(s)
    private const int DensityCurveSize = 4096;

    // Data
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;

    // Distribution Adjustments
    private readonly float[] _density;
    private readonly float[] _densityCurve;
    private readonly double _densityIntensity;
    private readonly double _centerCellSizeFactor;
    private readonly double _edgeCellSizeFactor;
    public readonly bool AllowBlackGaps;
    private readonly ImageDensityMode _mode;

    public VoronoiGapMask GapMask { get; set; }

    public IReadOnlyList<GapPolygon> GapPolygons => GapMask.Polygons;

    #region Constructors

    private DistributorImage(int width, int height, DistributorImageSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        _width = width;
        _height = height;
        _mode = settings.Mode;
        _densityIntensity = settings.DensityIntensity;
        _centerCellSizeFactor = settings.CenterCellSizeFactor;
        _edgeCellSizeFactor = settings.EdgeCellSizeFactor;
        _density = new float[width * height];
        _densityCurve = new float[DensityCurveSize];
        AllowBlackGaps = settings.AllowBlackGaps;
    }

    // ReSharper disable once UnusedMember.Global
    public DistributorImage(byte[] pixels, int width, int height, DistributorImageSettings settings)
        : this(width, height, settings)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < (long)width * height * 4)
            throw new ArgumentException("Pixel buffer must contain at least width * height * 4 bytes.",
                nameof(pixels));

        _pixels = pixels;
        GapMask = new VoronoiGapMask(_pixels, width, height);

        ValidateSpacing();
        BuildDensity();
    }

    // ReSharper disable once UnusedMember.Global
    public DistributorImage(Texture2D texture, DistributorImageSettings settings)
        : this(texture.Width, texture.Height, settings)
    {
        ArgumentNullException.ThrowIfNull(texture);
        int length = checked(_width * _height);
        _pixels = new byte[checked(length * 4)];
        texture.GetData(_pixels);
        GapMask = new VoronoiGapMask(_pixels, texture.Width, texture.Height); // After so pixel data is filled.

        ValidateSpacing();
        BuildDensity();
    }

    #endregion

    public void Generate(ref List<Point> points, int amount, double maxX, double maxY, double randomness)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfNegative(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxY);

        int count = points.Count;
        if (count >= amount)
            return;

        randomness = Clamp(randomness, 0.0, 1.0);
        double densityInfluence = 1.0 - randomness;

        /*
         * Average spacing for the requested amount.
         */
        double baseSpacing = Sqrt(maxX * maxY / amount) * 0.55;

        // minimumSpacing
        double _ = baseSpacing * _centerCellSizeFactor;
        double maximumSpacing = baseSpacing * _edgeCellSizeFactor;
        double centerX = maxX * 0.5;
        double centerY = maxY * 0.5;
        double maxRadius = Sqrt(centerX * centerX + centerY * centerY);

        /*
         * The grid is based on MAXIMUM spacing.
         *
         * This dramatically reduces the number of buckets compared
         * with using minimumSpacing as the grid size.
         */
        double cellSize = maximumSpacing / Sqrt(2.0);
        int gridWidth = Max(1, (int)Ceiling(maxX / cellSize));
        int gridHeight = Max(1, (int)Ceiling(maxY / cellSize));
        int gridLength = checked(gridWidth * gridHeight);

        /*
         * Head of linked list for each grid bucket.
         *
         * Multiple points may occupy the same bucket.
         */
        int[] heads = new int[gridLength];
        Array.Fill(heads, -1);

        /*
         * Each point gets one "next" link.
         *
         * We allocate enough room for the requested final count.
         */
        int[] next = new int[amount];
        Array.Fill(next, -1);

        /*
         * Cache point coordinates separately.
         *
         * This avoids repeatedly dereferencing Point objects/structs
         * while doing millions of distance checks.
         */
        double[] pointX = new double[amount];
        double[] pointY = new double[amount];

        for (int i = 0; i < count; i++)
        {
            Point point = points[i];

            pointX[i] = point.X;
            pointY[i] = point.Y;

            int gx = (int)(point.X / cellSize);
            int gy = (int)(point.Y / cellSize);

            if ((uint)gx >= (uint)gridWidth || (uint)gy >= (uint)gridHeight)
                continue;

            int bucket = gy * gridWidth + gx;

            next[i] = heads[bucket];
            heads[bucket] = i;
        }

        Random random = Random.Shared;
        int required = amount - count;

        /*
         * Maximum consecutive failures.
         *
         * This prevents pathological density maps from running forever.
         */
        int maxAttempts = Max(required * 100, 10_000);
        int attempts = 0;
        double spacingRange = _edgeCellSizeFactor - _centerCellSizeFactor;

        fixed (float* densityPtr = _density)
        fixed (float* curvePtr = _densityCurve)
        fixed (int* headsPtr = heads)
        fixed (int* nextPtr = next)
        fixed (double* pointXPtr = pointX)
        fixed (double* pointYPtr = pointY)
        {
            while (count < amount)
            {
                if (++attempts > maxAttempts)
                    break;

                double rx = random.NextDouble();
                double ry = random.NextDouble();
                int pixelX = (int)(rx * _width);
                int pixelY = (int)(ry * _height);
                int pixelIndex = pixelY * _width + pixelX;
                byte b = _pixels[pixelIndex * 4];
                byte g = _pixels[pixelIndex * 4 + 1];
                byte r = _pixels[pixelIndex * 4 + 2];
                if (AllowBlackGaps && (r | g | b) == 0)
                    continue;

                float density = densityPtr[pixelIndex];

                // Blend toward uniform density.
                density = (float)(density * densityInfluence + randomness);
                int densityIndex = (int)(density * 4095.0);
                densityIndex = densityIndex switch
                {
                    < 0 => 0,
                    >= DensityCurveSize => DensityCurveSize - 1,
                    _ => densityIndex
                };

                /*
                 * High density -> small spacing.
                 * Low density -> large spacing.
                 */
                double spacingFactor = _edgeCellSizeFactor - spacingRange * curvePtr[densityIndex];
                double x = rx * maxX;
                double y = ry * maxY;

                double dxCenter = x - centerX;
                double dyCenter = y - centerY;

                double radialDistance = Sqrt(dxCenter * dxCenter + dyCenter * dyCenter) / maxRadius;
                radialDistance = Clamp(radialDistance, 0.0, 1.0);

                // Smooth transition instead of a linear spacing jump.
                double smoothRadius = radialDistance * radialDistance * (3.0 - 2.0 * radialDistance);
                double cellSizeFactor =
                    _centerCellSizeFactor + (_edgeCellSizeFactor - _centerCellSizeFactor) * smoothRadius;
                double minimumDistance = baseSpacing * spacingFactor * cellSizeFactor;
                double minimumDistanceSquared = minimumDistance * minimumDistance;

                int gx = (int)(x / cellSize);
                int gy = (int)(y / cellSize);

                /*
                 * Because cellSize is based on maximumSpacing,
                 * only a small number of neighboring cells need
                 * to be checked.
                 *
                 * We calculate the required radius dynamically.
                 */
                int radius = (int)Ceiling(minimumDistance / cellSize);
                int minGX = Max(0, gx - radius);
                int maxGX = Min(gridWidth - 1, gx + radius);
                int minGY = Max(0, gy - radius);
                int maxGY = Min(gridHeight - 1, gy + radius);
                bool valid = true;

                int bucket;
                for (int yy = minGY; yy <= maxGY && valid; yy++)
                {
                    int row = yy * gridWidth;
                    for (int xx = minGX; xx <= maxGX; xx++)
                    {
                        bucket = row + xx;
                        int index = headsPtr[bucket];

                        while (index >= 0)
                        {
                            double dx = x - pointXPtr[index];
                            double dy = y - pointYPtr[index];
                            if (dx * dx + dy * dy < minimumDistanceSquared)
                            {
                                valid = false;
                                break;
                            }

                            index = nextPtr[index];
                        }

                        if (!valid)
                            break;
                    }
                }

                if (!valid)
                    continue;

                int pointIndex = count++;
                points.Add(new Point(x, y));

                // Next pointer.
                pointXPtr[pointIndex] = x;
                pointYPtr[pointIndex] = y;

                bucket = gy * gridWidth + gx;
                nextPtr[pointIndex] = headsPtr[bucket];
                headsPtr[bucket] = pointIndex;
                attempts = 0;
            }
        }
    }

    #region Helpers

    private void BuildDensity()
    {
        int length = _width * _height;
        const float Inv255 = 1.0f / 255.0f;

        for (int i = 0, p = 0; i < length; i++, p += 4)
        {
            byte b = _pixels[p];
            byte g = _pixels[p + 1];
            byte r = _pixels[p + 2];

            if (AllowBlackGaps && (r | g | b) == 0)
            {
                _density[i] = 0f;
                continue;
            }

            _density[i] = _mode switch
            {
                ImageDensityMode.Red => r * Inv255,
                ImageDensityMode.Green => g * Inv255,
                ImageDensityMode.Blue => b * Inv255,
                ImageDensityMode.Alpha => _pixels[p + 3] * Inv255,
                ImageDensityMode.Luminance => (0.2126f * r + 0.7152f * g + 0.0722f * b) * Inv255,
                _ => 0f
            };
        }

        /*
         * Precompute density curve.
         */
        for (int i = 0; i < DensityCurveSize; i++)
        {
            double density = i / 4095.0;
            _densityCurve[i] = (float)Pow(density, _densityIntensity);
        }
    }

    private void ValidateSpacing()
    {
        if (_centerCellSizeFactor <= 0)
            throw new ArgumentOutOfRangeException(nameof(_centerCellSizeFactor));

        if (_edgeCellSizeFactor < _centerCellSizeFactor)
            throw new ArgumentOutOfRangeException(nameof(_edgeCellSizeFactor));
    }

    #endregion

    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    public enum ImageDensityMode : byte
    {
        /// Brightness, aka the "traditional" height map.
        Luminance,

        Red,
        Green,
        Blue,
        Alpha
    }
}

[SuppressMessage("ReSharper", "ConvertToConstant.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Global")]
public class DistributorImageSettings
{
    private const double defaultIntensity = 1.1;
    private const double defaultMinSpacingFactor = 0.75;
    private const double defaultMaxSpacingFactor = 1.75;

    public DistributorImage.ImageDensityMode Mode = DistributorImage.ImageDensityMode.Luminance;
    public double DensityIntensity = defaultIntensity;
    public double CenterCellSizeFactor = defaultMinSpacingFactor;
    public double EdgeCellSizeFactor = defaultMaxSpacingFactor;
    public bool AllowBlackGaps = true;
}

public sealed class GapPolygon
{
    public IReadOnlyList<Vector2> Vertices { get; }

    public GapPolygon(List<Vector2> vertices)
    {
        Vertices = vertices;
    }
}

public sealed class VoronoiGapMask
{
    private readonly bool[] _mask;

    public int Width { get; }
    public int Height { get; }

    /// <summary>
    /// Closed contours representing the black regions.
    /// Coordinates are floating-point so smoothing does not reintroduce pixel jagging.
    /// </summary>
    public IReadOnlyList<GapPolygon> Polygons { get; }

    public VoronoiGapMask(
        byte[] pixels,
        int width,
        int height,
        int smoothingIterations = 2)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (pixels.Length < (long)width * height * 4)
            throw new ArgumentException(
                "Pixel buffer must contain at least width * height * 4 bytes.",
                nameof(pixels));

        Width = width;
        Height = height;

        _mask = new bool[width * height];

        for (int i = 0, p = 0; i < _mask.Length; i++, p += 4)
        {
            byte b = pixels[p];
            byte g = pixels[p + 1];
            byte r = pixels[p + 2];

            _mask[i] = (r | g | b) == 0;
        }

        Polygons = BuildPolygons(smoothingIterations);
    }

    private bool IsGap(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height && _mask[y * Width + x];

    #region Polygon Extraction

    private List<GapPolygon> BuildPolygons(int smoothingIterations)
    {
        var edges = new Dictionary<GridPoint, List<GridPoint>>();

        for (int y = 0; y < Height; y++)
        {
            for (int x = 0; x < Width; x++)
            {
                if (!IsGap(x, y))
                    continue;

                // Top
                if (y == 0 || !IsGap(x, y - 1))
                    AddEdge(
                        edges,
                        new GridPoint(x, y),
                        new GridPoint(x + 1, y));

                // Right
                if (x == Width - 1 || !IsGap(x + 1, y))
                    AddEdge(
                        edges,
                        new GridPoint(x + 1, y),
                        new GridPoint(x + 1, y + 1));

                // Bottom
                if (y == Height - 1 || !IsGap(x, y + 1))
                    AddEdge(
                        edges,
                        new GridPoint(x + 1, y + 1),
                        new GridPoint(x, y + 1));

                // Left
                if (x == 0 || !IsGap(x - 1, y))
                    AddEdge(
                        edges,
                        new GridPoint(x, y + 1),
                        new GridPoint(x, y));
            }
        }

        return TraceContours(edges, smoothingIterations);
    }

    private static void AddEdge(
        Dictionary<GridPoint, List<GridPoint>> edges,
        GridPoint start,
        GridPoint end)
    {
        if (!edges.TryGetValue(start, out List<GridPoint>? list))
        {
            list = [];
            edges.Add(start, list);
        }

        list.Add(end);
    }

    private List<GapPolygon> TraceContours(
        Dictionary<GridPoint, List<GridPoint>> edges,
        int smoothingIterations)
    {
        var unused = new HashSet<GridEdge>();

        foreach (KeyValuePair<GridPoint, List<GridPoint>> pair in edges)
        {
            foreach (GridPoint end in pair.Value)
                unused.Add(new GridEdge(pair.Key, end));
        }

        var polygons = new List<GapPolygon>();

        while (unused.Count > 0)
        {
            GridEdge first = default;
            bool found = false;

            foreach (GridEdge edge in unused)
            {
                first = edge;
                found = true;
                break;
            }

            if (!found)
                break;

            var contour = new List<GridPoint>();

            GridEdge current = first;
            GridPoint startPoint = first.Start;

            while (true)
            {
                if (!unused.Remove(current))
                    break;

                contour.Add(current.Start);

                GridPoint at = current.End;

                if (at == startPoint)
                    break;

                if (!TryGetNextEdge(
                        at,
                        current.Start,
                        edges,
                        unused,
                        out GridEdge next))
                {
                    break;
                }

                current = next;
            }

            if (contour.Count < 3)
                continue;

            // Remove redundant straight-line vertices first.
            contour = RemoveCollinearVertices(contour);

            if (contour.Count < 3)
                continue;

            var polygon = new List<Vector2>(contour.Count);

            foreach (GridPoint point in contour)
            {
                polygon.Add(new Vector2(point.X, point.Y));
            }

            // Smooth the pixel-derived contour.
            for (int i = 0; i < smoothingIterations; i++)
                polygon = Chaikin(polygon);

            if (polygon.Count >= 3)
                polygons.Add(new GapPolygon(polygon));
        }

        return polygons;
    }

    private static bool TryGetNextEdge(
        GridPoint current,
        GridPoint previous,
        Dictionary<GridPoint, List<GridPoint>> edges,
        HashSet<GridEdge> unused,
        out GridEdge next)
    {
        next = default;

        if (!edges.TryGetValue(current, out List<GridPoint>? candidates))
            return false;

        Vector2 incoming = new(
            current.X - previous.X,
            current.Y - previous.Y);

        double bestAngle = double.MaxValue;
        bool found = false;

        foreach (GridPoint candidate in candidates)
        {
            var edge = new GridEdge(current, candidate);

            if (!unused.Contains(edge))
                continue;

            Vector2 outgoing = new(
                candidate.X - current.X,
                candidate.Y - current.Y);

            if (outgoing.LengthSquared() <= 0f)
                continue;

            double cross =
                incoming.X * outgoing.Y -
                incoming.Y * outgoing.X;

            double dot =
                incoming.X * outgoing.X +
                incoming.Y * outgoing.Y;

            double angle = Math.Atan2(cross, dot);

            if (angle < 0)
                angle += Math.PI * 2.0;

            if (!found || angle < bestAngle)
            {
                bestAngle = angle;
                next = edge;
                found = true;
            }
        }

        return found;
    }

    #endregion

    #region Simplification / Smoothing

    private static List<GridPoint> RemoveCollinearVertices(
        List<GridPoint> polygon)
    {
        if (polygon.Count < 3)
            return polygon;

        var result = new List<GridPoint>(polygon.Count);

        for (int i = 0; i < polygon.Count; i++)
        {
            GridPoint previous =
                polygon[(i - 1 + polygon.Count) % polygon.Count];

            GridPoint current =
                polygon[i];

            GridPoint next =
                polygon[(i + 1) % polygon.Count];

            int dx1 = current.X - previous.X;
            int dy1 = current.Y - previous.Y;

            int dx2 = next.X - current.X;
            int dy2 = next.Y - current.Y;

            int cross = dx1 * dy2 - dy1 * dx2;

            if (cross != 0)
                result.Add(current);
        }

        return result;
    }

    private static List<Vector2> Chaikin(List<Vector2> polygon)
    {
        if (polygon.Count < 3)
            return polygon;

        var result = new List<Vector2>(polygon.Count * 2);

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];

            Vector2 q = Vector2.Lerp(a, b, 0.25f);
            Vector2 r = Vector2.Lerp(a, b, 0.75f);

            result.Add(q);
            result.Add(r);
        }

        return result;
    }

    #endregion

    private readonly record struct GridPoint(int X, int Y);

    private readonly record struct GridEdge(
        GridPoint Start,
        GridPoint End);
}