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
    private float[] _density;
    private readonly double _densityIntensity;

    // ReSharper disable once UnusedMember.Global
    public DistributorImage(byte[] pixels, int width, int height, ImageDensityMode mode = ImageDensityMode.Luminance)
    {
        _width = width;
        _height = height;
        _pixels = pixels;
        _mode = mode;

        BuildDensity();
    }

    public DistributorImage(Texture2D texture, 
        double densityIntensity = 1.6,
        ImageDensityMode mode = ImageDensityMode.Luminance)
    {
        ArgumentNullException.ThrowIfNull(texture);
        _width = texture.Width;
        _height = texture.Height;
        _pixels = new byte[_width * _height * 4];
        _mode = mode;
        _densityIntensity = densityIntensity;

        texture.GetData(_pixels);
        BuildDensity();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="points"></param>
    /// <param name="amount"></param>
    /// <param name="maxX"></param>
    /// <param name="maxY"></param>
    /// <param name="randomness">
    ///     How image-driven the point generation is. 0 is entirely image-driven, 1 is
    ///     entirely randomized.
    /// </param>
    /// <exception cref="InvalidOperationException"></exception>
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

        Random random = Random.Shared;

        /*
         * randomness = 0:
         *     entirely controlled by the image.
         *
         * randomness = 1:
         *     completely uniform spacing.
         */
        double densityInfluence = 1.0 - randomness;
        double area = maxX * maxY;

        /*
         * Average spacing for the requested number of points.
         */
        double baseSpacing = Sqrt(area / amount) * 0.55;

        /*
         * Grid cell is small enough that neighboring cells contain all possible conflicting points.
         */
        double cellSize = baseSpacing / Sqrt(2.0);

        int gridWidth = (int)Ceiling(maxX / cellSize);
        int gridHeight = (int)Ceiling(maxY / cellSize);

        int[] grid = new int[gridWidth * gridHeight];

        Array.Fill(grid, -1);

        /*
         * Insert existing points.
         */
        for (int i = 0; i < points.Count; i++)
        {
            Point point = points[i];
            int gx = Clamp((int)(point.X / cellSize), 0, gridWidth - 1);
            int gy = Clamp((int)(point.Y / cellSize), 0, gridHeight - 1);
            grid[gy * gridWidth + gx] = i;
        }

        int required = amount - count;
        int maxAttempts = Max(required * 500, 50_000);
        int attempts = 0;
        while (count < amount)
        {
            if (++attempts > maxAttempts)
                break;

            double rx = random.NextDouble();
            double ry = random.NextDouble();

            int pixelX = (int)(rx * _width);
            int pixelY = (int)(ry * _height);

            float imageDensity = _density[pixelY * _width + pixelX];

            /*
             * Blend toward uniform spacing.
             */
            double density = imageDensity * densityInfluence + (1.0 - densityInfluence);

            /*
             * IMPORTANT:
             *
             * Keep spacing variation relatively small.
             *
             * density 1 -> 0.70x
             * density 0 -> 1.30x
             *
             * This controls the overall density intense-ness of the graph.
             */
            double spacingFactor = _densityIntensity - density * 1.0;
            double minimumDistance = baseSpacing * spacingFactor;

            double x = rx * maxX;
            double y = ry * maxY;

            int gx = Clamp((int)(x / cellSize), 0, gridWidth - 1);
            int gy = Clamp((int)(y / cellSize), 0, gridHeight - 1);

            int searchRadius = (int)Ceiling(minimumDistance / cellSize);

            int minGX = Max(0, gx - searchRadius);
            int maxGX = Min(gridWidth - 1, gx + searchRadius);

            int minGY = Max(0, gy - searchRadius);
            int maxGY = Min(gridHeight - 1, gy + searchRadius);

            double minimumDistanceSquared =
                minimumDistance * minimumDistance;

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

                    if (!(dx * dx + dy * dy < minimumDistanceSquared))
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
        _density = new float[_width * _height];
        const float Inv255 = 1.0f / 255.0f;
        switch (_mode)
        {
            case ImageDensityMode.Red:
                for (int i = 0, p = 0; i < _density.Length; i++, p += 4)
                    _density[i] = _pixels[p + 2] * Inv255;
                break;
            case ImageDensityMode.Green:
                for (int i = 0, p = 0; i < _density.Length; i++, p += 4)
                    _density[i] = _pixels[p + 1] * Inv255;
                break;
            case ImageDensityMode.Blue:
                for (int i = 0, p = 0; i < _density.Length; i++, p += 4)
                    _density[i] = _pixels[p] * Inv255;
                break;
            case ImageDensityMode.Alpha:
                for (int i = 0, p = 0; i < _density.Length; i++, p += 4)
                    _density[i] = _pixels[p + 3] * Inv255;
                break;
            case ImageDensityMode.Luminance:
            default:
                for (int i = 0, p = 0; i < _density.Length; i++, p += 4)
                    _density[i] = (0.2126f * _pixels[p + 2] + 0.7152f * _pixels[p + 1] + 0.0722f * _pixels[p]) * Inv255;
                break;
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