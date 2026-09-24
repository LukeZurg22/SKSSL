using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework.Graphics;
using static System.Math;

namespace SKSSL.Utilities.Voronoi.PointDistributors;

public class DistributorImage : IPointDistributor
{
    private readonly byte[] _pixels;
    private readonly int _width;
    private readonly int _height;
    private readonly ImageDensityMode _mode;

    private readonly float[] _density;
    private readonly float[] _densityCurve;

    private readonly double _densityIntensity;
    private readonly double _minSpacingFactor;
    private readonly double _maxSpacingFactor;

    private const double defaultIntensity = 1.1;
    private const double defaultMinSpacingFactor = 0.75;
    private const double defaultMaxSpacingFactor = 4.00;

    public DistributorImage(
        byte[] pixels,
        int width,
        int height,
        ImageDensityMode mode = ImageDensityMode.Luminance,
        double densityIntensity = defaultIntensity,
        double minSpacingFactor = defaultMinSpacingFactor,
        double maxSpacingFactor = defaultMaxSpacingFactor)
    {
        ArgumentNullException.ThrowIfNull(pixels);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        if (pixels.Length < width * height * 4)
            throw new ArgumentException(
                "Pixel buffer must contain at least width * height * 4 bytes.",
                nameof(pixels));

        _width = width;
        _height = height;
        _pixels = pixels;
        _mode = mode;
        _densityIntensity = densityIntensity;
        _minSpacingFactor = minSpacingFactor;
        _maxSpacingFactor = maxSpacingFactor;

        BuildDensity();
    }

    public DistributorImage(
        Texture2D texture,
        ImageDensityMode mode = ImageDensityMode.Luminance,
        double densityIntensity = defaultIntensity,
        double minSpacingFactor = defaultMinSpacingFactor,
        double maxSpacingFactor = defaultMaxSpacingFactor)
    {
        ArgumentNullException.ThrowIfNull(texture);

        _width = texture.Width;
        _height = texture.Height;
        _mode = mode;
        _densityIntensity = densityIntensity;
        _minSpacingFactor = minSpacingFactor;
        _maxSpacingFactor = maxSpacingFactor;

        int length = _width * _height;
        _pixels = new byte[length * 4];
        _density = new float[length];
        _densityCurve = new float[4096];

        texture.GetData(_pixels);
        BuildDensity();
    }

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


        // Average spacing.
        double baseSpacing = Sqrt(maxX * maxY / amount) * 0.55;

        /*
         * The smallest possible spacing determines the grid resolution.
         *
         * This guarantees that a grid cell cannot contain two newly
         * generated points.
         */
        double minimumSpacing = baseSpacing * _minSpacingFactor;
        double cellSize = minimumSpacing / Sqrt(2.0);
        int gridWidth = (int)Ceiling(maxX / cellSize);
        int gridHeight = (int)Ceiling(maxY / cellSize);
        int gridLength = gridWidth * gridHeight;
        int[] grid = new int[gridLength];

        Array.Fill(grid, -1);
        for (int i = 0; i < count; i++)
        {
            Point point = points[i];
            int gx = (int)(point.X / cellSize);
            int gy = (int)(point.Y / cellSize);
            if ((uint)gx >= (uint)gridWidth || (uint)gy >= (uint)gridHeight)
                continue;

            grid[gy * gridWidth + gx] = i;
        }

        Random random = Random.Shared;
        int required = amount - count;

        /*
         * Maximum consecutive failures.
         */
        int maxAttempts = Max(required * 100, 10_000);
        int attempts = 0;

        /*
         * Precomputed constants.
         */
        double spacingRange =
            _maxSpacingFactor - _minSpacingFactor;

        while (count < amount)
        {
            if (++attempts > maxAttempts)
                break;

            double rx = random.NextDouble();
            double ry = random.NextDouble();

            int pixelX = (int)(rx * _width);
            int pixelY = (int)(ry * _height);

            /*
             * Blend image density toward uniform density.
             */
            float density = _density[pixelY * _width + pixelX];
            density = (float)(density * densityInfluence + randomness);

            /*
             * The expensive Pow() has been replaced by a lookup into
             * the precomputed nonlinear density curve.
             */
            int densityIndex = (int)(density * 4095.0);

            if (densityIndex > 4095)
                densityIndex = 4095;

            double densityCurve = _densityCurve[densityIndex];

            /*
             * Convert density to spacing.
             *
             * High density -> minimum spacing.
             * Low density  -> maximum spacing.
             */
            double spacingFactor = _maxSpacingFactor - spacingRange * densityCurve;
            double minimumDistance = baseSpacing * spacingFactor;

            double x = rx * maxX;
            double y = ry * maxY;

            int gx = (int)(x / cellSize);
            int gy = (int)(y / cellSize);

            /*
             * The candidate is always inside the requested bounds.
             */
            int searchRadius = (int)Ceiling(minimumDistance / cellSize);

            int minGX = Max(0, gx - searchRadius);
            int maxGX = Min(gridWidth - 1, gx + searchRadius);

            int minGY = Max(0, gy - searchRadius);
            int maxGY = Min(gridHeight - 1, gy + searchRadius);

            double distanceSquared = minimumDistance * minimumDistance;

            bool valid = true;

            for (int yy = minGY; yy <= maxGY && valid; yy++)
            {
                int row = yy * gridWidth;

                for (int xx = minGX; xx <= maxGX; xx++)
                {
                    int index = grid[row + xx];

                    if (index < 0)
                        continue;

                    Point other = points[index];

                    double dx = x - other.X;
                    double dy = y - other.Y;

                    if (!(dx * dx + dy * dy < distanceSquared))
                        continue;

                    valid = false;
                    break;
                }
            }

            if (!valid)
                continue;

            int pointIndex = points.Count;
            points.Add(new Point(x, y));
            grid[gy * gridWidth + gx] = pointIndex;
            count++;
            attempts = 0;
        }
    }

    private void BuildDensity()
    {
        int length = _width * _height;
        const float Inv255 = 1.0f / 255.0f;
        switch (_mode)
        {
            case ImageDensityMode.Red:
                for (int i = 0, p = 0; i < length; i++, p += 4)
                    _density[i] = _pixels[p + 2] * Inv255;
                break;
            case ImageDensityMode.Green:
                for (int i = 0, p = 0; i < length; i++, p += 4)
                    _density[i] = _pixels[p + 1] * Inv255;
                break;
            case ImageDensityMode.Blue:
                for (int i = 0, p = 0; i < length; i++, p += 4)
                    _density[i] = _pixels[p] * Inv255;
                break;
            case ImageDensityMode.Alpha:
                for (int i = 0, p = 0; i < length; i++, p += 4)
                    _density[i] = _pixels[p + 3] * Inv255;
                break;
            case ImageDensityMode.Luminance:
            default:
                for (int i = 0, p = 0; i < length; i++, p += 4)
                    _density[i] = (0.2126f * _pixels[p + 2] + 0.7152f * _pixels[p + 1] + 0.0722f * _pixels[p]) * Inv255;
                break;
        }

        /*
         * Precompute the nonlinear density curve.
         *
         * Math.Pow() is therefore performed only 4096 times instead
         * of potentially millions of times during Generate().
         */
        for (int i = 0; i < _densityCurve.Length; i++)
        {
            double density = i / 4095.0;
            _densityCurve[i] = (float)Pow(density, _densityIntensity);
        }
    }
}

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