using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    private readonly int _sourceWidth;
    private readonly int _sourceHeight;

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

    private DistributorImage(int sourceWidth, int sourceHeight, DistributorImageSettings settings)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _mode = settings.Mode;
        _densityIntensity = settings.DensityIntensity;
        _centerCellSizeFactor = settings.CenterCellSizeFactor;
        _edgeCellSizeFactor = settings.EdgeCellSizeFactor;
        _density = new float[sourceWidth * sourceHeight];
        _densityCurve = new float[DensityCurveSize];
        AllowBlackGaps = settings.AllowBlackGaps;
    }

    // ReSharper disable once UnusedMember.Global
    public DistributorImage(byte[] pixels, int sourceWidth, int sourceHeight, DistributorImageSettings settings)
        : this(sourceWidth, sourceHeight, settings)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        if (pixels.Length < (long)sourceWidth * sourceHeight * 4)
            throw new ArgumentException("Pixel buffer must contain at least width * height * 4 bytes.",
                nameof(pixels));

        _pixels = pixels;
        GapMask = new VoronoiGapMask(_pixels, sourceWidth, sourceHeight, 0);

        ValidateSpacing();
        BuildDensity();
    }

    // ReSharper disable once UnusedMember.Global
    public DistributorImage(Texture2D texture, DistributorImageSettings settings)
        : this(texture.Width, texture.Height, settings)
    {
        ArgumentNullException.ThrowIfNull(texture);
        int length = checked(_sourceWidth * _sourceHeight);
        _pixels = new byte[checked(length * 4)];
        texture.GetData(_pixels);
        GapMask = new VoronoiGapMask(_pixels, texture.Width, texture.Height); // After so pixel data is filled.

        ValidateSpacing();
        BuildDensity();
    }

    #endregion

    public void Generate(ref List<Point> points, uint amount, float maxX, float maxY, double randomness)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentOutOfRangeException.ThrowIfZero(amount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxY);

        int targetCount = checked((int)amount);
        int count = points.Count;
        if (count >= targetCount)
            return;

        randomness = Clamp(randomness, 0.0, 1.0);

        /*
         * randomness = 1:
         *     completely uniform distribution
         * randomness = 0:
         *     completely image-driven distribution
         */
        double densityInfluence = 1.0 - randomness;

        // Base spacing for the requested number of points.
        float baseSpacing = (float)(Sqrt((double)maxX * maxY / targetCount) * 0.55);

        /*
         * These are the actual minimum/maximum spacing multipliers
         * used by the algorithm.
         */
        float centerFactor = (float)_centerCellSizeFactor;
        float edgeFactor = (float)_edgeCellSizeFactor;
        float spacingRange = edgeFactor - centerFactor;

        /*
         * The largest possible spacing occurs when:
         *   density curve = 0
         *   radial factor = edgeFactor
         *   spacing = baseSpacing * edgeFactor * edgeFactor
         * This allows one to build the spatial grid around the actual
         * largest exclusion radius.
         */
        float maximumSpacing = baseSpacing * edgeFactor * edgeFactor;

        /*
         * A cell of maxSpacing / sqrt(2) guarantees that the
         * exclusion circle cannot span too many unrelated buckets.
         * Using float here substantially reduces memory bandwidth.
         */
        float gridCellSize = maximumSpacing * 0.7071067811865475f;
        int gridWidth = Max(1, (int)Ceiling(maxX / gridCellSize));
        int gridHeight = Max(1, (int)Ceiling(maxY / gridCellSize));
        int gridLength = checked(gridWidth * gridHeight);

        /*
         * Spatial hash:
         * heads[bucket] -> first point
         * next[point]   -> next point in bucket
         */
        int[] heads = new int[gridLength];
        Array.Fill(heads, -1);

        int[] next = new int[targetCount];
        Array.Fill(next, -1);

        /*
         * float is sufficient here because the final Point is already
         * being generated from screen/image coordinates.
         * This halves coordinate-cache bandwidth compared with double.
         */
        float[] pointX = new float[targetCount];
        float[] pointY = new float[targetCount];

        // Populate the spatial grid with existing points.
        for (int i = 0; i < count; i++)
        {
            Point point = points[i];
            float px = point.X;
            float py = point.Y;
            pointX[i] = px;
            pointY[i] = py;
            int gx = (int)(px / gridCellSize);
            int gy = (int)(py / gridCellSize);
            if ((uint)gx >= (uint)gridWidth || (uint)gy >= (uint)gridHeight)
                continue;

            int bucket = gy * gridWidth + gx;
            next[i] = heads[bucket];
            heads[bucket] = i;
        }

        int required = targetCount - count;

        // Don't allow a pathological density map to spin forever.
        int maxAttempts = Max(required > int.MaxValue / 100 ? int.MaxValue : required * 100, 10_000);
        int attempts = 0;
        float centerX = maxX * 0.5f;
        float centerY = maxY * 0.5f;

        /*
         * maxRadius is constant for the entire generation.
         * We use the reciprocal so the inner loop performs a
         * multiplication instead of a division.
         */
        float maxRadius = (float)Sqrt((double)centerX * centerX + (double)centerY * centerY);
        float inverseMaxRadius = 1.0f / maxRadius;
        fixed (float* densityPtr = _density)
        fixed (float* curvePtr = _densityCurve)
        fixed (int* headsPtr = heads)
        fixed (int* nextPtr = next)
        fixed (float* pointXPtr = pointX)
        fixed (float* pointYPtr = pointY)
        {
            Random random = Random.Shared;
            while (count < targetCount)
            {
                if (++attempts > maxAttempts)
                    break;

                // NextSingle() is noticeably cheaper than NextDouble() and avoids double -> float conversion.
                float rx = random.NextSingle();
                float ry = random.NextSingle();

                // Image lookup. Keep the calculation in integer space.
                int pixelX = (int)(rx * _sourceWidth);
                if (pixelX >= _sourceWidth)
                    pixelX = _sourceWidth - 1;

                int pixelY = (int)(ry * _sourceHeight);
                if (pixelY >= _sourceHeight)
                    pixelY = _sourceHeight - 1;

                int pixelIndex = pixelY * _sourceWidth + pixelX;
                
                // Density curve.
                float density = densityPtr[pixelIndex];
                density = (float)(density * densityInfluence + randomness);
                int densityIndex = (int)(density * (DensityCurveSize - 1));
                densityIndex = densityIndex switch
                {
                    < 0 => 0,
                    >= DensityCurveSize => DensityCurveSize - 1,
                    _ => densityIndex
                };

                /*
                 * Image-derived spacing factor.
                 * High density -> smaller spacing.
                 * Low density  -> larger spacing.
                 */
                float spacingFactor =
                    edgeFactor -
                    spacingRange * curvePtr[densityIndex];

                // Actual world position.
                float x = rx * maxX;
                float y = ry * maxY;

                /*
                 * Radial falloff.
                 * We calculate normalized squared distance first,
                 * then only perform the sqrt needed by smooth-step.
                 */
                float dxCenter = x - centerX;
                float dyCenter = y - centerY;

                float radiusSquared =
                    (dxCenter * dxCenter + dyCenter * dyCenter) *
                    (inverseMaxRadius * inverseMaxRadius);

                radiusSquared = MathF.Min(radiusSquared, 1.0f);

                /*
                 * Smooth-step:
                 * t²(3 - 2t)
                 */
                float smoothRadius =
                    radiusSquared *
                    radiusSquared *
                    (3.0f - 2.0f * radiusSquared);

                // Center -> edge spacing multiplier.
                float cellSizeFactor = centerFactor + spacingRange * smoothRadius;
                float minimumDistance = baseSpacing * spacingFactor * cellSizeFactor;
                float minimumDistanceSquared = minimumDistance * minimumDistance;

                // Spatial bucket.
                int gx = (int)(x / gridCellSize);
                int gy = (int)(y / gridCellSize);

                /*
                 * Number of buckets the exclusion radius can reach.
                 * Because gridCellSize is based on maximum spacing,
                 * this is normally very small.
                 */
                int radius = (int)Ceiling(minimumDistance / gridCellSize);
                int minGX = Max(0, gx - radius);
                int maxGX = Min(gridWidth - 1, gx + radius);
                int minGY = Max(0, gy - radius);
                int maxGY = Min(gridHeight - 1, gy + radius);
                bool valid = true;

                /*
                 * Collision test.
                 * This is the hottest section of the entire generator.
                 */
                for (int yy = minGY; yy <= maxGY && valid; yy++)
                {
                    int bucket = yy * gridWidth + minGX;
                    for (int xx = minGX; xx <= maxGX; xx++, bucket++)
                    {
                        int index = headsPtr[bucket];
                        while (index >= 0)
                        {
                            float dx =
                                x - pointXPtr[index];

                            float dy =
                                y - pointYPtr[index];

                            // Squared-distance test avoids sqrt.
                            if (dx * dx + dy * dy < minimumDistanceSquared)
                            {
                                valid = false;
                                break;
                            }

                            index = nextPtr[index];
                        }
                    }
                }

                if (!valid)
                    continue;

                // Accept point.
                int pointIndex = count++;

                points.Add(new Point(x, y));

                pointXPtr[pointIndex] = x;
                pointYPtr[pointIndex] = y;

                int bucketIndex = gy * gridWidth + gx;
                nextPtr[pointIndex] = headsPtr[bucketIndex];
                headsPtr[bucketIndex] = pointIndex;

                // Reset rejection counter after successful placement.
                attempts = 0;
            }
        }
    }

    #region Helpers

    private void BuildDensity()
    {
        int length = _sourceWidth * _sourceHeight;
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
    public bool AllowBlackGaps = false;
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

    public VoronoiGapMask(byte[] pixels, int width, int height, int smoothingIterations = 2)
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
        for (int x = 0; x < Width; x++)
        {
            if (!IsGap(x, y))
                continue;

            // Top
            if (y == 0 || !IsGap(x, y - 1))
                AddEdge(edges, new GridPoint(x, y), new GridPoint(x + 1, y));

            // Right
            if (x == Width - 1 || !IsGap(x + 1, y))
                AddEdge(edges, new GridPoint(x + 1, y), new GridPoint(x + 1, y + 1));

            // Bottom
            if (y == Height - 1 || !IsGap(x, y + 1))
                AddEdge(edges, new GridPoint(x + 1, y + 1), new GridPoint(x, y + 1));

            // Left
            if (x == 0 || !IsGap(x - 1, y))
                AddEdge(edges, new GridPoint(x, y + 1), new GridPoint(x, y));
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

    private static List<GapPolygon> TraceContours(Dictionary<GridPoint, List<GridPoint>> edges, int smoothingIterations)
    {
        var unused = new HashSet<GridEdge>();
        foreach (var pair in edges)
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
            bool closed = false;

            while (true)
            {
                if (!unused.Remove(current))
                    break;

                contour.Add(current.Start);

                GridPoint at = current.End;

                if (at == startPoint)
                {
                    closed = true;
                    break;
                }

                if (!TryGetNextEdge(at, current.Start, edges, unused, out GridEdge next))
                {
                    break;
                }

                current = next;
            }

            if (!closed || contour.Count < 3)
                continue;

            // Remove redundant straight-line vertices first.
            contour = RemoveCollinearVertices(contour);

            if (contour.Count < 3)
                continue;

            var polygon = new List<Vector2>(contour.Count);
            polygon.AddRange(contour.Select(point => new Vector2(point.X, point.Y)));

            // Smooth the pixel-derived contour.
            polygon = SmoothPolygon(polygon, smoothingIterations);

            if (polygon.Count >= 3)
                polygons.Add(new GapPolygon(polygon));
        }

        return polygons;
    }

    private static List<Vector2> SmoothPolygon(
        List<Vector2> polygon,
        int smoothingIterations)
    {
        if (polygon.Count < 3 || smoothingIterations <= 0)
            return polygon;

        double area = Abs(SignedArea(polygon));

        // Preserve very small features.
        if (area < 16.0)
            return polygon;

        int iterations =
            area < 64.0
                ? Min(smoothingIterations, 1)
                : smoothingIterations;

        for (int i = 0; i < iterations; i++)
            polygon = Chaikin(polygon);

        return polygon;
    }

    private static double SignedArea(IReadOnlyList<Vector2> polygon)
    {
        double area = 0.0;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[(i + 1) % polygon.Count];

            area +=
                (double)a.X * b.Y -
                (double)b.X * a.Y;
        }

        return area * 0.5;
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

            double angle = Atan2(cross, dot);

            if (angle < 0)
                angle += PI * 2.0;

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